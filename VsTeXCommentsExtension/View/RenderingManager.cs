﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;

namespace VsTeXCommentsExtension.View
{
    public class RenderingManager : RenderingManager<HtmlRenderer.Input, RendererResult>, IRenderingManager
    {
        private readonly Queue<Request> tempQueue = new Queue<Request>();

        public RenderingManager(IRenderer<HtmlRenderer.Input, RendererResult> renderer)
            : base(renderer)
        {
        }

        protected override void OnRequestAddition(Queue<Request> queue, HtmlRenderer.Input newRequest)
        {
            //We want to remove already existing requests with same content as newRequest if it's for same textView.
            //Example: We change zoom and everything is going to be rerendered. While rendering we change
            //         zoom again and it is useless to finish rendering with old zoom.

            while (queue.Count > 0)
            {
                var existingRequest = queue.Dequeue();
                if (existingRequest.Input.Content != newRequest.Content ||
                    existingRequest.Input.TextView != newRequest.TextView)
                {
                    tempQueue.Enqueue(existingRequest);
                }
            }

            while (tempQueue.Count > 0)
            {
                queue.Enqueue(tempQueue.Dequeue());
            }
        }
    }

    public class RenderingManager<TInput, TResult> : IRenderingManager<TInput, TResult>
    {
        private readonly IRenderer<TInput, TResult> renderer;
        private readonly Queue<Request> requests = new Queue<Request>();
        private readonly ManualResetEvent manualResetEvent = new ManualResetEvent(false);

        public RenderingManager(IRenderer<TInput, TResult> renderer)
        {
            this.renderer = renderer;

            var thread = new Thread(ProcessQueue);
            thread.Name = $"{typeof(RenderingManager).FullName}_{nameof(ProcessQueue)}";
            thread.Start();
        }

        public void RenderAsync(TInput input, Action<TResult> renderingDoneCallback)
        {
            lock (requests)
            {
                Debug.WriteLine(nameof(RenderAsync));
                OnRequestAddition(requests, input);
                requests.Enqueue(new Request(input, renderingDoneCallback));
                manualResetEvent.Set();
            }
        }

        private void ProcessQueue()
        {
            while (true)
            {
                while (requests.Count > 0)
                {
                    Request request;
                    lock (requests)
                    {
                        request = requests.Dequeue();
                        if (requests.Count == 0) manualResetEvent.Reset();
                    }

                    var result = renderer.Render(request.Input);
                    request.ResultCallback(result);
                }

                manualResetEvent.WaitOne();
            }
        }

        protected virtual void OnRequestAddition(Queue<Request> queue, TInput newRequest)
        {
        }

        protected struct Request
        {
            public readonly TInput Input;
            public readonly Action<TResult> ResultCallback;

            public Request(TInput input, Action<TResult> resultCallback)
            {
                Input = input;
                ResultCallback = resultCallback;
            }
        }
    }
}
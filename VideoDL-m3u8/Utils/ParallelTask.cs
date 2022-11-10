using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using VideoDL_m3u8.Events;
using VideoDL_m3u8.Exceptions;

namespace VideoDL_m3u8.Utils
{
    internal class ParallelTask
    {
        public static async Task Run<TSource>(IEnumerable<TSource> source,
            Func<TSource, CancellationToken, Task> worker,
            int maxThreads, int delay, int retry,
            IProgress<ProgressEventArgs>? progress = null,
            CancellationToken token = default)
        {
            var cts = CancellationTokenSource.CreateLinkedTokenSource(token);

            var queue = new ConcurrentQueue<TSource>(source);

            var tasks = new List<Task>();

            var totalCount = queue.Count;
            var finishCount = 0;
            var retryCount = 0;
            var totalBytes = 0L;
            var finishBytes = 0L;
            var speed = 0;
            var eta = 0;

            while (true)
            {
                if (tasks.Count == 0 && queue.Count == 0)
                {
                    break;
                }

                if (!token.IsCancellationRequested)
                {
                    if (tasks.Count < maxThreads)
                    {
                        if (queue.TryDequeue(out var next))
                        {
                            var task = Task.Run(async () =>
                            {
                                async Task func()
                                {
                                    try
                                    {
                                        await worker(next, cts.Token);
                                    }
                                    catch (OperationCanceledException)
                                    {
                                    }
                                    catch (Exception ex)
                                    {
                                        retryCount++;
                                        if (retryCount > retry)
                                            throw new OverRetryException(ex);
                                        await Task.Delay(delay);
                                        await func();
                                    }
                                }
                                await func();
                            });
                            tasks.Add(task);
                            await Task.Delay(delay);
                            continue;
                        }
                    }
                }
                
                if (tasks.Count == 0)
                {
                    token.ThrowIfCancellationRequested();
                    continue;
                }

                var finish = await Task.WhenAny(tasks.ToArray());

                try
                {
                    await finish;
                }
                catch(OverRetryException)
                {
                    cts.Cancel();
                    try
                    {
                        Task.WaitAll(tasks.ToArray());
                    }
                    catch 
                    {
                    }
                    throw;
                }
                finally
                {
                    tasks.Remove(finish);
                }
            }
        }
    }
}
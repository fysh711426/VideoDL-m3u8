using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using VideoDL_m3u8.Exceptions;

namespace VideoDL_m3u8.Utils
{
    internal class ParallelTask
    {
        public static async Task Run<TSource>(IEnumerable<TSource> source,
            Func<TSource, CancellationToken, Task> worker,
            int maxThreads, int delay, int maxRetry, Action<int> onRetry,
            CancellationToken token = default)
        {
            var cts = CancellationTokenSource.CreateLinkedTokenSource(token);

            var queue = new ConcurrentQueue<TSource>(source);

            var tasks = new List<Task>();

            var stop = false;
            var locker = new object();
            var retry = 0;

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
                                        throw;
                                    }
                                    catch (Exception ex)
                                    {
                                        lock (locker)
                                        {
                                            if (!stop)
                                            {
                                                if (retry >= maxRetry)
                                                {
                                                    stop = true;
                                                    throw new OverRetryException(ex);
                                                }
                                                retry++;
                                            }
                                        }
                                        if (!stop)
                                        {
                                            await Task.Delay(delay);
                                            onRetry(retry);
                                            await func();
                                            return;
                                        }
                                        throw;
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
                catch (OperationCanceledException)
                {
                }
                finally
                {
                    tasks.Remove(finish);
                }
            }
        }
    }
}
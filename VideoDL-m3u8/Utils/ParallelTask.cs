using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace VideoDL_m3u8.Utils
{
    internal class ParallelTask
    {
        public static async Task Run<TSource>(IEnumerable<TSource> source,
            Func<TSource, Task> worker,
            int maxThreads, int delay, int retry,
            CancellationToken token = default)
        {
            var queue = new ConcurrentQueue<TSource>(source);

            var tasks = new List<Task>();

            while (true)
            {
                if (tasks.Count == 0 && queue.Count == 0)
                {
                    break;
                }

                if (tasks.Count < maxThreads)
                {
                    if (queue.TryDequeue(out var next))
                    {
                        var task = Task.Run(async () =>
                        {
                            try
                            {
                                await worker(next);
                            }
                            catch (Exception)
                            {
                                retry--;
                                if (retry < 0)
                                    throw new Exception("Over retry times.");
                                queue.Enqueue(next);
                            }
                        });
                        tasks.Add(task);
                        await Task.Delay(delay);
                        continue;
                    }
                }

                if (tasks.Count == 0)
                {
                    continue;
                }

                var finish = await Task.WhenAny(tasks.ToArray());

                try
                {
                    await finish;
                }
                catch
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
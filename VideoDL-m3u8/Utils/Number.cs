using System;

namespace VideoDL_m3u8.Utils
{
    internal class Number
    {
        public static double Safe(Func<double> func) 
        {
            try
            {
                return func();
            }
            catch
            {
                return 0;
            }
        }
    }
}

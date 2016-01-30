using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KinectRecorder
{
    /// <summary>
    /// Source: http://www.mikeadev.net/2013/06/generic-method/
    /// </summary>
    public static class MathHelper
    {
        /// <summary>
        /// Clamps value within desired range
        /// </summary>
        /// <param name="value">Value to be clamped</param>
        /// <param name="min">Min range</param>
        /// <param name="max">Max range</param>
        /// <returns>Clamped value within range</returns>
        public static T Clamp<T>(T value, T min, T max)
            where T : IComparable<T>
        {
            T result = value;
            if (result.CompareTo(max) > 0)
                result = max;
            if (result.CompareTo(min) < 0)
                result = min;

            return result;
        }
    }
}

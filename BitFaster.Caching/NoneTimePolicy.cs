using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitFaster.Caching
{
    public class NoneTimePolicy : ITimePolicy
    {
        public static readonly TimeSpan Infinite = new TimeSpan(0, 0, 0, 0, -1);

        public static NoneTimePolicy Instance = new NoneTimePolicy();

        public bool CanExpire => false;

        public TimeSpan TimeToLive => Infinite;

        public void TrimExpired()
        {
        }
    }
}

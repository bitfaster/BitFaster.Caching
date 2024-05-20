namespace SeqLock
{
    internal class TornProgramSeqLock
    {
        public void run()
        {
            Task.Run((Action) setter);
            Task.Run((Action) checker);

            Console.WriteLine("Press <ENTER> to stop");
            Console.ReadLine();
        }

        void setter()
        {
            while (true)
            {
                d1.Write(VALUE1);
                d1.Write(VALUE2);
            }
        }

        void checker()
        {
            for (int count = 0;; ++count)
            {
                var t = d1.Read();

                if (t != VALUE1 && t != VALUE2)
                    Console.WriteLine("SeqLockValue is torn after {0} iterations: {1}", count, t);
            }
        }

        LruItem<int, Decimal> d1 = new LruItem<int, decimal>(1, VALUE1);

        const Decimal VALUE1 = 1m;
        const Decimal VALUE2 = 10000000000m;
    }
}

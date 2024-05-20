namespace SeqLock
{
    // https://stackoverflow.com/questions/23262513/reproduce-torn-reads-of-decimal-in-c-sharp
    internal class TornProgram
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
                d = VALUE1;
                d = VALUE2;
            }
        }

        void checker()
        {
            for (int count = 0;; ++count)
            {
                var t = d;

                if (t != VALUE1 && t != VALUE2)
                    Console.WriteLine("Value is torn after {0} iterations: {1}", count, t);
            }
        }

        Decimal d;

        const Decimal VALUE1 = 1m;
        const Decimal VALUE2 = 10000000000m;
    }
}

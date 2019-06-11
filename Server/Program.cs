
using System;

namespace Server
{
    class Program
    {
        static void Main(string[] args)
        {
            try
            {
                new Server(8696);

            }
            catch (Exception e)
            {
                Console.WriteLine(e);

                Console.ReadLine();
            }
        }
    }
}

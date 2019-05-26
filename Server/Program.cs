using System;

namespace Server
{
    class Program
    {
        static void Main(string[] args)
        {
            int port = 8696;
            var server = new Server(port);
        }
    }
}

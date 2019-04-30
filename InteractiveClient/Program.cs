using System;
using System.Net;
using System.Threading;

namespace InteractiveClient
{
    class Program
    {
        static void Main(string[] args)
        {
            var console = new InteractiveConsole();
            console.Start();
        }
    }
}

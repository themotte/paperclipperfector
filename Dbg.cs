using System;

namespace paperclipperfector
{
    internal static class Dbg
    {
        internal static void Inf(string str)
        {
            Console.ForegroundColor = ConsoleColor.Gray;
            Console.WriteLine(str);
        }

        internal static void Wrn(string str)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine(str);
            Console.ForegroundColor = ConsoleColor.Gray;
        }

        internal static void Err(string str)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine(str);
            Console.ForegroundColor = ConsoleColor.Gray;
        }

        internal static void Ex(Exception e)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine(e.ToString());
            Console.ForegroundColor = ConsoleColor.Gray;
        }
    }
}
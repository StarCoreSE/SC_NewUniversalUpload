using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SC_NewUniversalUpload.Utilities
{
    public class ArgumentParser
    {
        private readonly Dictionary<string, string> _taggedArguments = new Dictionary<string, string>();
        public List<string> UntaggedArguments = new List<string>();

        public ArgumentParser(string[] args)
        {
            Console.WriteLine($"Parsing {args.Length} arguments...");
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i].StartsWith('-'))
                    _taggedArguments[args[i]] = args[i+1].StartsWith('-') ? "" : SanitizeArgument(args[++i]);
                else
                    UntaggedArguments.Add(SanitizeArgument(args[i]));
            }

            foreach (var kvp in _taggedArguments)
                Console.WriteLine($"*    {kvp.Key} \"{kvp.Value}\"");
            foreach (var arg in UntaggedArguments)
                Console.WriteLine($"*    \"{arg}\"");
        }

        /// <summary>
        /// Remove leading and trailing quotes from an argument with spaces.
        /// </summary>
        /// <param name="argument"></param>
        /// <returns></returns>
        private string SanitizeArgument(string argument)
        {
            if (argument.StartsWith('"') && argument.EndsWith('"'))
            {
                argument = argument.Remove(0, 1).Remove(argument.Length, 1);
            }

            return argument;
        }

        public string? this[string key]
        {
            get => _taggedArguments!.GetValueOrDefault(key, null);
            private set => throw new NotImplementedException();
        }
    }
}

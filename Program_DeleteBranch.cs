using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SC_NewUniversalUpload
{
    internal partial class Program
    {
        // This doesn't work :(
        public void DeleteBranch()
        {
            var branchModIds = LocateAllModInfos(Arguments["--repo"]).Where(path => path.Contains(Branch)).Select(RetrieveModId);
            Console.WriteLine("Workshop mods to be deleted:");
            foreach (var modId in branchModIds)
            {
                Console.Write("- " + modId);

                Console.WriteLine(" (finished)");
            }
        }
    }
}

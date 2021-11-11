using System.Threading.Tasks;

namespace Rattletrap
{
    class Program
    {
        public static Task Main(string[] args)
            => Startup.RunAsync(args);
    }
}

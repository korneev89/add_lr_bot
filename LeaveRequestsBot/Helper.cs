using System.IO;
using System.Reflection;

namespace LeaveRequestsBot
{
	public static class Helper
	{
		public static FileInfo CreateFileInfoToAssemblyDirectory(string name)
		{
			return new FileInfo(Path.Combine(
					Path.GetDirectoryName(
						Assembly.GetExecutingAssembly().Location),
						name));
		}

		public static string FullPathToFile(string filename)
		{
			return CreateFileInfoToAssemblyDirectory(filename).FullName;
		}
	}
}

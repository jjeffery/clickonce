using System.Linq;

namespace ClickOnce
{
	public static class CompatibleFramework
	{
		public const string V35 = "3.5";
		public const string V40Client = "4.0-client";
		public const string V40Full = "4.0-full";
		public const string V45Full = "4.5-full";
		public const string V451Full = "4.5.1-full";
		public const string V452Full = "4.5.2-full";
		public const string V46Full = "4.6-full";
		public const string V461Full = "4.6.1-full";

		public static string[] All = {V35, V40Client, V40Full, V45Full, V451Full, V452Full, V46Full, V461Full};
		public const string Default = V35;

		public static bool IsValid(string v)
		{
			return All.FirstOrDefault(ver => ver == v) != null;
		}
	}
}

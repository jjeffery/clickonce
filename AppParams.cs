using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using NDesk.Options;

namespace ClickOnce
{
	public class AppParams
	{
		// this program name
		public string ProgramName { get; set; }

		// name of the application
		public string ApplicationName { get; set; }
		public string Publisher { get; set; }
		public string Product { get; set; }
		public string ExeName { get; set; }
		public string ApplicationVersion { get; set; }
		public string CertificateHash { get; set; }
		public string CertificateName { get; set; }
		public string FromDirectory { get; set; }
		public string ToDirectory { get; set; }
		public bool CreateDesktopShortcut { get; set; }
		public string TimestampUrl { get; set; }
		public bool InstallApplication { get; set; }
		public string DesktopIconFile { get; set; }
		public bool DisableAutoUpdate { get; set; }
		public string ProcessorArchitecture { get; set; }

		// should the files have a .deploy added to them
		public bool MapFileExtensions { get; set; }

		// should the app be given URL parameters
		public bool TrustUrlParameters { get; set; }

		public bool Help { get; set; }
		public int Verbosity { get; set; }

		public string ManifestDirectory { get; set; }
		public string ManifestFileName { get; set; }
		public string ManifestFilePath { get; set; }

		public string DeploymentFileName { get; set; }
		public string DeploymentFilePath { get; set; }

		public IList<string> CompatibleFrameworks { get; private set; }

		public IDictionary<string, List<string>> Groups { get; private set; }

		private readonly OptionSet optionSet;
		private readonly IList<string> errors = new List<string>();
		private readonly IList<string> extras;
		
		public AppParams()
		{
			// default values
			TimestampUrl = "http://timestamp.verisign.com/scripts/timstamp.dll";
			ProgramName = Assembly.GetEntryAssembly().GetName().Name;
			ProcessorArchitecture = "msil";

			CertificateName = "Software Projects Pty Ltd";
			CompatibleFrameworks = new List<string>();
			Groups = new Dictionary<string, List<string>>();
		}

		public AppParams(IEnumerable<string> args)
			: this()
		{
			optionSet = new OptionSet {
				{"n|name=", "* Application name", v => ApplicationName = v},
				{"x|exe=", "* Application executable file", v => ExeName = v},
				{"v|version=", "* Application version", v => ApplicationVersion = v},
				{"h|hash=", "  Certificate hash", v => CertificateHash = v},
				{"c|certificate=", "  Certificate name", v => CertificateName = v},
				{"f|from=", "* From directory", v => FromDirectory = v},
				{"t|to=", "* To directory", v => ToDirectory = v},
				{"p|publisher=", "  Publisher", v => Publisher = v}, {
					"framework=",
					"  Framework (" + string.Join(", ", CompatibleFramework.All) + ") can specified multiple times, default=" +
					CompatibleFramework.Default,
					v => CompatibleFrameworks.Add(v)
				},
				{"product=", "  Product", v => Product = v},
				{"i|install", "  Install application", v => InstallApplication = v != null}, 
				{
					"trust-url-parameters", "  Application should be given the activation URL",
					v => TrustUrlParameters = true
				}, 
				{
					"map-file-extensions", "  Files should end with .deploy file extension", v => MapFileExtensions = true
				}, 
				{
					"disable-auto-update", "  The click once application should not automatically check for updates.",
					v => DisableAutoUpdate = true
				},
				{"create-desktop-shortcut", "  Create a desktop shortcut icon", v => CreateDesktopShortcut = true}, 
				{
					"desktop-icon-file=", "  Specify the desktop shortcut icon file (must exist in 'From' directory)",
					v => DesktopIconFile = v
				}, 
				{
					"processor-architecture=", "  Specify the processor architecture (msil, x86, amd64, ia64)",
					v => ProcessorArchitecture = v
				},
				{ "group=", "  Assign a file to a group (format group:file)", AssignToGroup },
				{"u|timestamp-url=", "  Timestamp URL", v => TimestampUrl = v},
				{"verbose", "  Increase verbosity", var => Verbosity++},
				{"?|help", "  Show this help message", v => Help = (v != null)},
			};

			try
			{
				extras = optionSet.Parse(args);
				Validate();
			}
			catch (OptionException ex)
			{
				errors.Add(ex.Message);
			}
		}

		private void AssignToGroup(string s)
		{
			var array = s.Split(new[] {':', ','}, 2);
			if (array.Length != 2)
			{
				errors.Add("Invalid group arguments. Example: --group=GroupName:FileName.dll");
				return;
			}

			var groupName = array[0];
			var fileName = array[1];

			List<string> list;
			if (!Groups.TryGetValue(groupName, out list))
			{
				list = new List<string>();
				Groups.Add(groupName, list);
			}
			list.Add(fileName);
		}

		public bool HasErrors
		{
			get { return errors.Count > 0; }
		}

		public void ShowErrors(TextWriter writer)
		{
			if (errors.Count > 0)
			{
				writer.WriteLine("{0}: {1}", ProgramName, errors[0]);
				for (int index = 1; index < errors.Count; ++index)
				{
					writer.WriteLine("    " + errors[index]);
				}
				writer.WriteLine("For usage information type {0} --help", ProgramName);
			}
		}

		public void ShowHelp(TextWriter writer)
		{
			writer.WriteLine("Usage: {0} [ options ]", ProgramName);
			writer.WriteLine("Generate a click-once deployment package.");
			writer.WriteLine("https://github.com/jjeffery/clickonce");
			writer.WriteLine();
			writer.WriteLine("Options:");
			optionSet.WriteOptionDescriptions(writer);
			writer.WriteLine("(Items marked * are mandatory)");
		}

		public void Validate()
		{
			if (CompatibleFrameworks.Count == 0)
			{
				CompatibleFrameworks.Add(CompatibleFramework.Default);
			}

			if (String.IsNullOrEmpty(ApplicationName))
			{
				errors.Add("ApplicationName must be specified");
			}

			if (String.IsNullOrEmpty(ExeName))
			{
				errors.Add("ExeName must be specified");
			}

			if (String.IsNullOrEmpty(ApplicationVersion))
			{
				errors.Add("ApplicationVersion must be specified");
			}

			if (String.IsNullOrEmpty(FromDirectory))
			{
				errors.Add("FromDirectory must be specified");
				return;
			}

			if (!Directory.Exists(FromDirectory))
			{
				errors.Add("Cannot find FromDirectory: " + FromDirectory);
			}

			if (String.IsNullOrEmpty(ToDirectory))
			{
				errors.Add("ToDirectory must be specified");
			}

			if (String.IsNullOrEmpty(CertificateHash))
			{
				if (String.IsNullOrEmpty(CertificateName))
				{
					errors.Add("Must specify --hash or --certificate");
				}
			}

			if (!String.IsNullOrEmpty(DesktopIconFile))
			{
				if (DesktopIconFile == Path.GetFileName(DesktopIconFile))
				{
					string fullPath = Path.Combine(FromDirectory, DesktopIconFile);
					if (!File.Exists(fullPath))
					{
						errors.Add("Cannot find desktop icon file: " + fullPath);
					}
					if (Path.GetExtension(DesktopIconFile).ToLowerInvariant() != ".ico")
					{
						errors.Add("Desktop icon file must have .ico suffix");
					}
				}
				else
				{
					errors.Add("Desktop icon file should not include the directory -- it must be in the 'From' directory");
				}
			}

			// validate compatible frameworks
			{
				var invalidFramework = false;
				var client40 = false;
				var full40 = false;
				foreach (var framework in CompatibleFrameworks)
				{
					switch (framework)
					{
						case CompatibleFramework.V35:
							break;
						case CompatibleFramework.V40Client:
							client40 = true;
							break;
						case CompatibleFramework.V40Full:
							full40 = true;
							break;
						default:
							if (!CompatibleFramework.IsValid(framework))
							{
								errors.Add("Unknown framework: " + framework);
								invalidFramework = true;
							}
							break;
					}
				}
				if (invalidFramework)
				{
					errors.Add("Valid frameworks: " + string.Join(", ", CompatibleFramework.All));
				}
				else if (client40 && !full40)
				{
					// if client compatible, then it is full compatible
					CompatibleFrameworks.Add(CompatibleFramework.V40Full);
				}
			}

			foreach (var s in extras)
			{
				errors.Add("Unknown argument: " + s);
			}
		}

		public void UpdateDerivedValues()
		{
			if (HasErrors)
			{
				throw new InvalidOperationException("Cannot update derived values if app params are not valid");
			}

			ManifestDirectory = Path.Combine(ToDirectory, ApplicationVersion);
			ManifestFileName = ExeName + ".manifest";
			ManifestFilePath = Path.Combine(ManifestDirectory, ManifestFileName);

			DeploymentFileName = Path.GetFileNameWithoutExtension(ExeName) + ".application";
			DeploymentFilePath = Path.Combine(ToDirectory, DeploymentFileName);
		}
	}
}

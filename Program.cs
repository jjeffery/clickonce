using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Xml;

namespace ClickOnce
{
	internal class Program
	{
		private static AppParams appParams;

		private static void Main(string[] args)
		{
			appParams = new AppParams(args);
			SetUpPath();
			if (appParams.Help)
			{
				appParams.ShowHelp(Console.Out);
				Environment.Exit(0);
				return;
			}
			if (appParams.HasErrors)
			{
				appParams.ShowErrors(Console.Error);
				Environment.Exit(1);
				return;
			}

			if (appParams.Verbosity > 0)
			{
				Console.WriteLine("Application name:    " + appParams.ApplicationName);
				Console.WriteLine("EXE name:            " + appParams.ExeName);
				Console.WriteLine("Application version: " + appParams.ApplicationVersion);
				Console.WriteLine("From directory:      " + appParams.FromDirectory);
				Console.WriteLine("To directory:        " + appParams.ToDirectory);
				Console.WriteLine("Certificate hash:    " + appParams.CertificateHash);
				Console.WriteLine("Certificate name:    " + appParams.CertificateName);
				Console.WriteLine("Timestamp URL:       " + appParams.TimestampUrl);
				Console.WriteLine("Create desktop icon: " + appParams.CreateDesktopShortcut);
				Console.WriteLine("Desktop icon file:   " + appParams.DesktopIconFile);
			}

			try
			{
				DoWork();
			}
			catch (ProgramFailedException ex)
			{
				Console.Error.WriteLine("{0}: {1}", appParams.ProgramName, ex.Message);
				Environment.ExitCode = 1;
			}
			catch (Exception ex)
			{
				Console.Error.WriteLine("{0}: Unexpected error occurred", appParams.ProgramName);
				Console.Error.WriteLine(ex);
				Environment.ExitCode = 1;
			}
		}

		private static void SetUpPath()
		{
			var subdirectories = new[]
			                     	{
										@"Microsoft SDKs\Windows\V7.0A\Bin",
										@"Microsoft SDKs\Windows\v6.0A\Bin",
										@"Microsoft SDKs\Windows\v7.0A\Bin\NETFX 4.0 Tools",
										@"Microsoft SDKs\Windows\v8.1A\Bin\NETFX 4.5.1 Tools",
										@"Microsoft SDKs\Windows\v10.0A\Bin\NETFX 4.6 Tools",
										@"Microsoft SDKs\Windows\v10.0A\Bin\NETFX 4.6.1 Tools",
										@"Microsoft SDKs\Windows\v10.0A\Bin\NETFX 4.7.1 Tools",
										@"Microsoft SDKs\Windows\v10.0A\Bin\NETFX 4.7.2 Tools",
									};
			// because we are an x86 program, this should be the program files (x86) folder.
			var programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);

			foreach (var subdirectory in subdirectories)
			{
				var directory = Path.Combine(programFilesX86, subdirectory);
				if (Directory.Exists(directory))
				{
					var filePath = Path.Combine(directory, "mage.exe");
					if (File.Exists(filePath))
					{
						Environment.SetEnvironmentVariable("PATH",
						                                   directory + ";" 
														   + Environment.GetEnvironmentVariable("PATH"));
						Log(2, "Added '{0}' to PATH", directory);
					}
				}
			}
		}

		private static void DoWork()
		{
			appParams.UpdateDerivedValues();
			CreateManifestDirectory();
			CreateManifestFile();
			ModifyManifestFile();
			SignFile(appParams.ManifestFilePath);
			CreateDeploymentFile();
			ModifyDeploymentFile();
			SignFile(appParams.DeploymentFilePath);
			CopyFiles();
		}

		private static void CreateManifestDirectory()
		{
			if (!Directory.Exists(appParams.ManifestDirectory))
			{
				Log(1, "Creating directory " + appParams.ManifestDirectory);
				Directory.CreateDirectory(appParams.ManifestDirectory);
			}
		}

		private static void CreateManifestFile()
		{
			Log(1, "Creating manifest file: " + appParams.ManifestFilePath);
			List<string> args = new List<string>
			                    	{
			                    		"-New", "Application",
			                    		"-ToFile", appParams.ManifestFilePath,
                                        "-Name", appParams.ApplicationName,
                                        "-Processor", appParams.ProcessorArchitecture,
                                        "-Version", appParams.ApplicationVersion,
                                        "-FromDirectory", appParams.FromDirectory,
                                        "-TrustLevel", "FullTrust",
			                    	};
			Exec("mage.exe", args);
		}

		public static void ModifyManifestFile()
		{
			Log(1, "Modifying manifest file: " + appParams.ManifestFilePath);
			XmlNameTable nt = new NameTable();
			XmlDocument doc = new XmlDocument(nt);
			XmlNamespaceManager nsmgr = new XmlNamespaceManager(nt);
			nsmgr.AddNamespace("asmv1", "urn:schemas-microsoft-com:asm.v1");
			nsmgr.AddNamespace("asmv2", "urn:schemas-microsoft-com:asm.v2");
			doc.Load(appParams.ManifestFilePath);

			if (!String.IsNullOrEmpty(appParams.DesktopIconFile))
			{
				XmlElement descriptionElem = doc.SelectSingleNode("/asmv1:assembly/asmv1:description", nsmgr) as XmlElement;
				if (descriptionElem == null)
				{
					XmlElement assemblyIdentityElem =
						doc.SelectSingleNode("/asmv1:assembly/asmv1:assemblyIdentity", nsmgr) as XmlElement;
					if (assemblyIdentityElem == null)
					{
						throw new ApplicationException("Cannot find assemblyIdentity element in manifest file");
					}

					descriptionElem = doc.CreateElement("asmv1", "description", "urn:schemas-microsoft-com:asm.v1");
					assemblyIdentityElem.ParentNode.InsertAfter(descriptionElem, assemblyIdentityElem);
					descriptionElem.SetAttribute("iconFile", "urn:schemas-microsoft-com:asm.v2", appParams.DesktopIconFile);
				}
			}

			var runtimeElement = doc.SelectSingleNode("//asmv2:assemblyIdentity[@name='Microsoft.Windows.CommonLanguageRuntime']", nsmgr) as XmlElement;
			if (runtimeElement != null)
			{
				if (appParams.CompatibleFrameworks.Contains(CompatibleFramework.V35))
				{
					runtimeElement.SetAttribute("version", "2.0.50727.0");
				}
				else
				{
					runtimeElement.SetAttribute("version", "4.0.30319.0");
				}
			}

			// If one or more files have been assigned to an optional load group.
			if (appParams.Groups.Count > 0)
			{
				// Build up a set of all file names assigned to a group. This will help detect any file
				// names specified on the command line that do not exist in the application.
				var allFileNames = new HashSet<string>(StringComparer.InvariantCultureIgnoreCase);
				foreach (var keyValuePair in appParams.Groups)
				{
					foreach (var fileName in keyValuePair.Value)
					{
						allFileNames.Add(fileName);
					}
				}

				var dependentAssemblyElements = doc.SelectNodes("//asmv2:dependentAssembly", nsmgr);
				if (dependentAssemblyElements != null)
				{
					foreach (XmlElement dependentAssemblyElement in dependentAssemblyElements)
					{
						var codebase = dependentAssemblyElement.GetAttribute("codebase");
						if (string.IsNullOrEmpty(codebase))
						{
							continue;
						}

						foreach (var keyValuePair in appParams.Groups)
						{
							var groupName = keyValuePair.Key;
							var fileNames = keyValuePair.Value;

							foreach (var fileName in fileNames)
							{
								if (codebase.Equals(fileName, StringComparison.InvariantCultureIgnoreCase))
								{
									dependentAssemblyElement.SetAttribute("group", groupName);
									var dependencyElement = (XmlElement) dependentAssemblyElement.ParentNode;
									// ReSharper disable PossibleNullReferenceException
									// Should never be null
									dependencyElement.SetAttribute("optional", "true");
									// ReSharper restore PossibleNullReferenceException

									allFileNames.Remove(fileName);
								}
							}
						}
					}
				}

				var fileElements = doc.SelectNodes("//asmv2:file", nsmgr);
				if (fileElements != null)
				{
					foreach (XmlElement fileElement in fileElements)
					{
						var name = fileElement.GetAttribute("name");
						if (string.IsNullOrEmpty(name))
						{
							continue;
						}

						foreach (var keyValuePair in appParams.Groups)
						{
							var groupName = keyValuePair.Key;
							var fileNames = keyValuePair.Value;

							foreach (var fileName in fileNames)
							{
								if (name.Equals(fileName, StringComparison.InvariantCultureIgnoreCase))
								{
									fileElement.SetAttribute("group", groupName);
									fileElement.SetAttribute("optional", "true");
									allFileNames.Remove(fileName);
								}
							}
						}
					}
				}

				if (allFileNames.Count == 1)
				{
					var message = string.Format("Non-existent file is assigned to group: {0}", allFileNames.First());
					throw new ProgramFailedException(message);
				}

				if (allFileNames.Count > 1)
				{
					var sb = new StringBuilder("Non-existent files are assigned to groups:");
					sb.AppendLine();
					foreach (var fileName in allFileNames)
					{
						sb.AppendLine("   " + fileName);
					}
					throw new ProgramFailedException(sb.ToString());
				}
			}

			doc.Save(appParams.ManifestFilePath);
		}

		private static void CreateDeploymentFile()
		{
			Log(1, "Creating deployment file: ", appParams.DeploymentFilePath);
			List<string> args = new List<string>
			                    	{
                                        "-New",
                                        "Deployment",
                                        "-Install", appParams.InstallApplication ? "true" : "false",
                                        "-ToFile", appParams.DeploymentFilePath,
                                        "-Name", appParams.ApplicationName,
                                        "-Version", appParams.ApplicationVersion,
                                        "-AppManifest", appParams.ManifestFilePath,
                                        "-Processor", appParams.ProcessorArchitecture,                                        
				
			                    	};
			
			if(!string.IsNullOrEmpty(appParams.Publisher))
			{
				args.Add("-Publisher");
				args.Add(appParams.Publisher);
			}

			Exec("mage.exe", args);
		}

		private static void ModifyDeploymentFile()
		{
			Log(1, "Modifying deployment file: " + appParams.DeploymentFilePath);
			XmlDocument doc = new XmlDocument();
			XmlNamespaceManager nsmgr = new XmlNamespaceManager(doc.NameTable);
			nsmgr.AddNamespace("asmv1", "urn:schemas-microsoft-com:asm.v1");
			nsmgr.AddNamespace(String.Empty, "urn:schemas-microsoft-com:asm.v2");

			doc.Load(appParams.DeploymentFilePath);
			XmlElement rootElement = doc.DocumentElement;
			if (rootElement == null)
			{
				throw new ApplicationException("Missing document element in deployment file");
			}
			foreach (XmlElement childElement in rootElement.ChildNodes)
			{
				if ((childElement.Name == "description") && !string.IsNullOrEmpty(appParams.Product))
				{
					childElement.SetAttribute("asmv2:product", appParams.Product);
				}
				if (childElement.Name == "deployment")
				{
					if (appParams.MapFileExtensions)
					{
						childElement.SetAttribute("mapFileExtensions", "true");
					}
					if (appParams.TrustUrlParameters)
					{
						childElement.SetAttribute("trustURLParameters", "true");
					}
					if (appParams.CreateDesktopShortcut)
					{
						childElement.SetAttribute("createDesktopShortcut", "urn:schemas-microsoft-com:clickonce.v1", "true");
					}

					if(appParams.DisableAutoUpdate)
					{
						foreach (XmlElement deploymentChildElement in childElement.ChildNodes)
						{
							if(deploymentChildElement.Name == "subscription")
							{
								childElement.RemoveChild(deploymentChildElement);
								break;
							}
						}
					}
				}
				if (childElement.Name == "compatibleFrameworks")
				{
					childElement.RemoveAll();
					foreach (var framework in appParams.CompatibleFrameworks)
					{
						var frameworkElement = childElement.OwnerDocument.CreateElement("framework", "urn:schemas-microsoft-com:clickonce.v2");
						switch (framework)
						{
							case CompatibleFramework.V35:
								frameworkElement.SetAttribute("targetVersion", "3.5");
								frameworkElement.SetAttribute("supportedRuntime", "2.0.50727");
								break;
							case CompatibleFramework.V40Client:
								frameworkElement.SetAttribute("targetVersion", "4.0");
								frameworkElement.SetAttribute("profile", "Client");
								frameworkElement.SetAttribute("supportedRuntime", "4.0.30319");
								break;
							case CompatibleFramework.V40Full:
								frameworkElement.SetAttribute("targetVersion", "4.0");
								frameworkElement.SetAttribute("profile", "Full");
								frameworkElement.SetAttribute("supportedRuntime", "4.0.30319");
								break;
							case CompatibleFramework.V45Full:
								frameworkElement.SetAttribute("targetVersion", "4.5");
								frameworkElement.SetAttribute("profile", "Full");
								frameworkElement.SetAttribute("supportedRuntime", "4.0.30319");
								break;
							case CompatibleFramework.V451Full:
								frameworkElement.SetAttribute("targetVersion", "4.5.1");
								frameworkElement.SetAttribute("profile", "Full");
								frameworkElement.SetAttribute("supportedRuntime", "4.0.30319");
								break;
							case CompatibleFramework.V452Full:
								frameworkElement.SetAttribute("targetVersion", "4.5.2");
								frameworkElement.SetAttribute("profile", "Full");
								frameworkElement.SetAttribute("supportedRuntime", "4.0.30319");
								break;
							case CompatibleFramework.V46Full:
								frameworkElement.SetAttribute("targetVersion", "4.6");
								frameworkElement.SetAttribute("profile", "Full");
								frameworkElement.SetAttribute("supportedRuntime", "4.0.30319");
								break;
							case CompatibleFramework.V461Full:
								frameworkElement.SetAttribute("targetVersion", "4.6.1");
								frameworkElement.SetAttribute("profile", "Full");
								frameworkElement.SetAttribute("supportedRuntime", "4.0.30319");
								break;
							default:
								throw new ApplicationException("Unknown framework: " + framework);
						}
						childElement.AppendChild(frameworkElement);
					}

				}
			}
			doc.Save(appParams.DeploymentFilePath);
		}

		private static void CopyFiles()
		{
			foreach (string filePath in Directory.GetFiles(appParams.FromDirectory))
			{
				string destFileName = Path.GetFileName(filePath);
				if (appParams.MapFileExtensions)
				{
					destFileName += ".deploy";
				}
				string destFilePath = Path.Combine(appParams.ManifestDirectory, destFileName);
				File.Copy(filePath, destFilePath, true);
				Log(2, "Copied file: " + destFileName);
			}
		}

		private static string GetCertificateHash()
		{
			if (!string.IsNullOrEmpty(appParams.CertificateHash))
			{
				return appParams.CertificateHash;
			}

			// TODO: allow specification of certificate store location
			var store = new X509Store(StoreName.My, StoreLocation.CurrentUser);
			store.Open(OpenFlags.ReadOnly);
			X509Certificate2 matchingCert = null;
			var now = DateTime.Now;
			foreach (var cert in store.Certificates)
			{
				var commonName = GetCommonName(cert.SubjectName);
				if (commonName.Equals(appParams.CertificateName, StringComparison.InvariantCultureIgnoreCase))
				{
					if (matchingCert == null || !IsCertValidOn(matchingCert, now))
					{
						Log(2, "Found certificate with CN={0}, NotBefore={1}, NotAfter={2}, Hash={3}",
							commonName, cert.NotBefore, cert.NotAfter, cert.GetCertHashString());
						matchingCert = cert;
					}
					else
					{
						if (IsCertValidOn(cert, now) && cert.NotAfter > matchingCert.NotAfter)
						{
							// only replace if both are valid and the new one is valid for longer
							Log(2, "Replacing previous certificate with CN={0}, NotBefore={1}, NotAfter={2}, Hash={3}",
								commonName, cert.NotBefore, cert.NotAfter, cert.GetCertHashString());
							matchingCert = cert;
						}
					}
				}
			}

			if (matchingCert == null)
			{
				throw new ProgramFailedException("Certificate not found: " + appParams.CertificateName);
			}

			if (!IsCertValidOn(matchingCert, now))
			{
				throw new ProgramFailedException("Certificate not valid: " + appParams.CertificateName);
			}

			return matchingCert.GetCertHashString();
		}

		private static bool IsCertValidOn(X509Certificate2 cert, DateTime now)
		{
			return now >= cert.NotBefore && now <= cert.NotAfter;
		}

		// Simple extractor for CN=...
		// Assumes there is only one CN. Returns the first one.
		private static string GetCommonName(X500DistinguishedName dn)
		{
			foreach (var item in (dn.Name ?? string.Empty).Split(','))
			{
				if (item.StartsWith("CN=", StringComparison.OrdinalIgnoreCase))
				{
					return item.Substring(3);
				}
			}
			return string.Empty;
		}

		private static void SignFile(string filePath)
		{
			var certificateHash = GetCertificateHash();

			Log(1, "Signing file: " + filePath);
			List<string> args = new List<string>
			                    	{
			                    		"-Sign",
			                    		filePath,
			                    		"-CertHash",
			                    		certificateHash,
			                    		"-TimeStampUri",
			                    		appParams.TimestampUrl,
			                    	};
			Exec("mage.exe", args);
		}

		private static void Exec(string programName, IEnumerable<string> args)
		{
			StringBuilder arguments = new StringBuilder();
			foreach (string arg in args)
			{
				if (arguments.Length > 0)
				{
					arguments.Append(' ');
				}
				if (arg.Contains(" ") || arg.Contains("\t"))
				{
					arguments.Append('"');
					arguments.Append(arg);
					arguments.Append('"');
				}
				else
				{
					arguments.Append(arg);
				}
			}
			Process process = new Process
								{
									StartInfo = new ProcessStartInfo(programName, arguments.ToString()) { UseShellExecute = false }
								};
			Log(2, "Starting program: {0} {1}", programName, process.StartInfo.Arguments);
			process.Start();
			process.WaitForExit();
			Log(2, "Program {0} finished with exit code {1}", programName, process.ExitCode);
			if (process.ExitCode != 0)
			{
				throw new ProgramFailedException(String.Format("Program {0} exited with code {1}", programName, process.ExitCode));
			}
		}

		private static void Log(int level, string format, params object[] args)
		{
			if (appParams.Verbosity >= level)
			{
				Console.WriteLine(format, args);
			}
		}

		private class ProgramFailedException : Exception
		{
			public ProgramFailedException(string message) : base(message) { }
		}
	}
}
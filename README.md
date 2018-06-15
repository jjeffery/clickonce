# clickonce

This repo contains the source code to the `clickonce` CLI for creating
ClickOnce deployment packages.

The `clickonce` utility calls out to [mage.exe](https://bit.ly/2JKpc9f) 
tool to create the deployment package.

## Usage

```
Usage: clickonce [ options ]
Generate a click-once deployment package.

Options:
  -n, --name=VALUE           * Application name
  -x, --exe=VALUE            * Application executable file
  -v, --version=VALUE        * Application version
  -h, --hash=VALUE             Certificate hash
  -c, --certificate=VALUE      Certificate name
  -f, --from=VALUE           * From directory
  -t, --to=VALUE             * To directory
  -p, --publisher=VALUE        Publisher
      --framework=VALUE        Framework (3.5, 4.0-client, 4.0-full, 4.5-ful-
                               l, 4.5.1-full, 4.5.2-full, 4.6-full, 4.6.1-full)
                               can specified multiple times, default=3.5
      --product=VALUE          Product
  -i, --install                Install application
      --trust-url-parameters   Application should be given the activation URL
      --map-file-extensions    Files should end with .deploy file extension
      --disable-auto-update    The click once application should not
                               automatically check for updates.
      --create-desktop-shortcut
                               Create a desktop shortcut icon
      --desktop-icon-file=VALUE
                               Specify the desktop shortcut icon file (must
                               exist in 'From' directory)
      --processor-architecture=VALUE
                               Specify the processor architecture (msil, x86,
                               amd64, ia64)
      --group=VALUE            Assign a file to a group (format group:file)
  -u, --timestamp-url=VALUE    Timestamp URL
      --verbose                Increase verbosity
  -?, --help                   Show this help message
(Items marked * are mandatory)
```

## Deprecated

This repo was moved to github from a private subversion repo in 2018, but it is very 
unlikely that it would be useful for new projects.

* This utility was developed circa 2009 for use with projects that at the time were
  being build using the [NAnt](http://nant.sourceforge.net/) build tool. More recent
  projects that build ClickOnce deployment packages are likely to make use of MSBuild.

* ClickOnce is not necessarily the best choice for deploying Windows desktop applications.
  Consider Squirrel (https://github.com/Squirrel/Squirrel.Windows), or 
  Chcolatey (https://chocolatey.org).


# Visual Studio for Mac Extension

This is the add-in for Visual Studio for Ma that enables Meadow projects to be built and deployed on Mac. 

## Installation 

To install the add-in:

#### Install the latest version of [Visual Studio for Mac](https://visualstudio.microsoft.com/downloads/).

#### Install the add-in from the extensions/add-ins manager: 
 1. open VS Mac, open the **Visual Studio** > **Extensions** menu and select the **Gallery** tab.
 2. Select **All repositories** and Search for `Meadow` (you may have to click the **Refresh** button).
 3. Select the Meadow extension and then click **install** on the right.
 4. Follow any additional installation instructions.

## Building the Add-in from Source

 1. Install the latest version of [Visual Studio for Mac](https://visualstudio.microsoft.com/downloads/).
 2. Install the **AddinMaker** maker from the extensions/add-ins manager (follow installation instructions above and search for `AddinMaker` instead).
 3. Clone project from GitHub and open the `VS4Mac_Meadow_Extension.sln` solution with VS Mac.
 4. Make sure all the nuget packages are restored and then start debugging, which will open a new instance of VS Mac that has the Add-in that just built enabled.

### Creating an Add-in Package

To create an addin package (`.mpack`), first, build the add-in in release mode, and then run the `CreateAddinPackage.sh` script from terminal.

#### Publishing

When publishing, make sure to bump the version number in the `/Properties/AddinInfo.cs` file, as well as the `AssemblyInfo.cs` file.

## License

Released under the [Apache 2 license](license.md).

## Authors

Bryan Costanich, Adrian Stevens

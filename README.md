# AMRIE
## Antimicrobial Resistance Test Interpretation Engine

This software was created by the [WHONET](https://whonet.org) development team to facilitate
the interpretation of antibiotic test measurements according to the published guidelines.
For more information about the guidelines, please visit their websites: [CLSI](https://clsi.org/), [EUCAST](https://www.eucast.org/), [SFM](https://www.sfm-microbiologie.org/)

You can install this software on Windows using the command `winget install WHONET.AMRIE` or by downloading the latest release from GitHub.

There are three sets of needs that the solution aims to support:
1. Interactive use through one of the two desktop applications
2. Command line use
3. Library integration with an external system

The library code is written in .NET 8 with no additional dependencies. It can be built for and integrated with software on any platform which supports .NET 8,
which includes Windows, GNU/Linux and Mac.

The interactive and command line interfaces exercise various interpretation features, demonstrating how one might incorporate the library into their systems. The user
interfaces can be used to generate interpretations, but we expect that most groups will have a greater need for library integration to provide the interpretations for
their own work.

The interactive applications may be used to generate interpretations for an entire data file, or explore ad hoc interpretations and view filtered resources.
The ad hoc functionality allows you to select from lists of organisms and antibiotics, and provide an associated test measurement to interpret.
To generate interpretations for an entire data file using the graphical interface or command line application, the input data file must use WHONET naming conventions for the 
data columns, measurements, organism codes, etc. Sample data, configuration files and system resources (such as organism, antibiotic and breakpoint lists)
are provided in the installation package's `Resources` folder.

The command line interface is another option which can be used to generate interpretations. The command line application can be called as needed from another application, from a batch script, etc.

We also provide SQL queries that demonstrate certain concepts, but which are
not used by the code. These concepts are implemented in C# to assist others if
they choose to make their own implementations, and also to eliminate any
dependence on a certain database technology which could restrict cross-platform compatibility.

To improve processing performance without the help of the database, the system has automatic caching built in,
and includes a way to "preheat" the cache to maximize interpretation performance if additional information about the input data set can be provided ahead of time.
To observe the caching behavior's effect, notice the difference in processing speed between the first and second times you press the "Interpret data file" button on the graphical user interface.
Subsequent runs of the system will utilize the breakpoint cache, greatly improving processing performance. The same behavior will automatically be present when the library is integrated in a
3rd party system.

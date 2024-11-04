# MassRTPSearch
Searches for RTP (Return To Player) of list of games in input file (1 per line, no commas) using Perplexity, and outputs sorted csv. Built for advantage play research and trying Perplexity's API. Lists of all games on casinos can often be found in network traffic or queried for by sending custom requests and cleaned up with simple scripts (ask an LLM).

This relies on Perplexity to report the RTP of games. For games with adjustable RTP, it often misses min RTPs and only reports max RTPs. Double check any results.

## Installation

### Build from source

1. Install the .NET 8.0 SDK from https://dotnet.microsoft.com/download

2. Install Git from https://git-scm.com/downloads

3. Open a terminal and clone the repository:

```git clone https://github.com/JTJabba/MassRTPSearch.git```

4. Navigate to the cloned repository:

```cd MassRTPSearch```

5. Build the project in release mode:

```dotnet build -c Release```

6. Copy everything in bin/Release/net8.0/ to a new folder outside of the git clone folder and run from there

7. Can delete the old folder git clone created

### Download Build

1. Download the latest release from https://github.com/JTJabba/MassRTPSearch/releases

2. Extract the zip file

## Usage
1. Open a terminal in the program's folder

2. Run with `.\MassRTPSearch.exe <path to games.txt> <perplexity api key>`

If you have issues:
- Make sure you have .Net 8.0 Runtime installed
- Make sure the model in config isn't deprecated

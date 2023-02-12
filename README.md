# KNX Unlocker

A little C# tootl that uses the offical KNX SDK to bruteforce a locked KNX device. 

## KNX and the BCU Key

A locked KNX device is a device where the BCU key is set and you want to unlock it. The BCU key is only a four byte key but it needs to be either guessed or extracted from the memory. Some devices (like some of Merten) do not validate the key and recovery is not needed. 


## Can I reset my device without the BCU Key 

Probablly not. Some vendors can reflash their devices. 

## How does this tool work ?

You need either a KNX GW or KNX USB Interface and the device address. The KNX Unlocker is basicly a bruteforcer. Bruteforcing the key is tricky becuase the key is one of 2^32 possiblieties and the KNX BUS is quite slow (~9-11 tries per seconds)
The bruteforcer supports three modes 
* level 1: tryies some of the default keys
* level 2: tryies a list of keys from all kind of wordlists (keys.txt) or a custom wordlist
* level 3: tryies the whole key space  (*with 10 tries per seconds, can take up to 13 years*)

Some other features: 
* The bruteforce stores the progress in a text file with to continue where it left off.  

## Usage 

```bash
# test if your device is locked
# discover a KNX GW or USB device for bruteforce
# start the bruteforcer
```

## Pre built binaries

The tool has been prebuilt and its so large because its a single executuable including the matching dotnet runtime + default keys. 

## Built

The project is based on dotnet core and you should only need the dotnet SDK to build it.

```bash
# build it
dotnet build
# run the tool with --help
dotnet run -- --help
# build a standalone binary
dotnet publish -c Release
```

## TODOs
* Write more documentation about the bruteforce idea
* Add the keys to binary 
* Check for typos
* Test the tooling more

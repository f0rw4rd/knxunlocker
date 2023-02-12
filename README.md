# KNX Unlocker

A little C# tootl that uses the offical KNX SDK to bruteforce a locked KNX device, whcich is a device where the BCU key is set and you want to unlock it.
The BCU key is only a four byte key but it needs to be either guessed or extracted from the memory. Some devices (like some of Merten) do not validate the key and recovery is not needed. 


This tool was used by [Limes Security](https://limessecurity.com/en/knxlock/) and might be able to guess an easy BCU key. 

## Can I reset my device without the BCU Key 

Probablly not. Some vendors can reflash their devices. 

## How does this tool work ?

You need either a KNX GW or KNX USB Interface and the device address. The KNX Unlocker is basicly a bruteforcer. Bruteforcing the key is tricky becuase the key is one of 2^32 possiblieties and the KNX BUS is quite slow (~9-11 tries per seconds)
The bruteforcer supports three modes 
* level 1: tries some of the default keys
* level 2: tries a list of keys from all kind of wordlists (keys.txt) or a custom wordlist or your choice.
* level 3: tries the whole key space (*with 10 tries per seconds, can take up to 13 years because the KNX bus is really slow*)

Some other features: 
* The bruteforce stores the progress in a text file with to continue where it left off.  
* The tool can disocver network KNX GWs and usb KNX dongles and output the needed connection string

## Usage 

```bash
# you need to know the KNX device address (three digits) beforhand. This can be easily detected by clicking the program button on the device
# discover a KNX GW or USB device for bruteforce tot use
# test if the device is locked at all
# start the bruteforcer 
# if the bruteforcer found the key, you can reset the key via the ETS (KNX engineering Software)
```

## The bruteforce takes too long, what are my other options

* You can try recover the key from memory of the device by dumping the flash: This heavily depends on the device and if hardware protection is enabled (might need advanced attacks like glitchting)
* If you have multiple devices with the same key (assumption) you can accelerate the cracking process by using multiple KNX buses (Multiple cracking attempts on the same bus are not going to be faster). Other tools might be more usefull. 
* Contact the vendor 

## Pre built binaries

The tool has been prebuilt and its so large because its a single executuable including the matching dotnet runtime + default keys. Checkout the Releases section. 

## Built

The project is based on dotnet core and you should only need the dotnet SDK to build it.

```bash
# build it
dotnet build
# run the tool with --help
dotnet run -- --help
# build a standalone binary
dotnet publish -c Release -r win-x64 --self-contained
dotnet publish -c Release -r linux-x64 --self-contained
```

## Contribution 

If you found a bug, want a new feature, got a question, feel free to open a issue anytime. 
Pull requests are welcome :-)

## Othter informations
* [knxmap](https://github.com/takeshixx/knxmap) can bruteforce keys as well but has limited support for KNX GWs and USB Dongles

## TODOs
* Write more documentation about the bruteforce idea
* Add the keys to binary 
* Check for typos
* Test the tooling more

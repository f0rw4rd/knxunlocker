using System;
using System.IO;
using System.Text;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using CommandLine;
using Knx.Bus.Common.Configuration;
using Knx.Bus.Common.Exceptions;
using Knx.Bus.Common.KnxIp;
using Knx.Falcon.Sdk;
using ShellProgressBar;
using knxunlock;

namespace knxunlock
{

    public class KNXKeyFoundException : Exception
    {

	public uint Key { get; }
	public byte Level { get; }

        public KNXKeyFoundException()
        {
        }

        public KNXKeyFoundException(uint key, byte level)
        {
		Key = key;
		Level = level;
        }

        public KNXKeyFoundException(string message, Exception inner)
            : base(message, inner)
        {
        }
    }

    public class KeySpaceIterator
    {
        private const uint key_space_size = uint.MaxValue;
        private const uint increment = 88099901; // prime number
        private uint _last;
        public uint seed;

        public KeySpaceIterator(uint seed)
        {
            _last = seed;
            this.seed = seed;
        }

        public uint Next()
        {
            _last = (_last + increment) % key_space_size;
            return _last;
        }

    }

    public class CommandLineOptions
    {
        [Option(shortName: 'U', longName: "usblist", Required = false, HelpText = "List all the connected and supported KNX USB devices", Default = false)]
        public bool ListUsb { get; set; }

        [Option(shortName: 'N', longName: "networklist", Required = false, HelpText = "List all the discoverable KNX IP Gateways", Default = false)]
        public bool ListNetwork { get; set; }

        [Option(longName: "defaultkeys", Required = false, HelpText = "Try the default keys", Default = false)]
        public bool TryDefaultKeys { get; set; }

        [Option(longName: "dictionaryKeys", Required = false, HelpText = "Try the dict keys", Default = false)]
        public bool TryDictionaryKeys { get; set; }

        [Option(longName: "full", Required = false, HelpText = "Try the all keys", Default = false)]
        public bool TryAllKeys { get; set; }

        [Option(longName: "middle", Required = false, HelpText = "Generate key based on middle key (see https://github.com/f0rw4rd/knxunlocker/issues/2)", Default = false)]
        public bool GenerateKeysFromMiddleKey { get; set; }

        [Option(shortName: 'b', longName: "benchmark", Required = false, HelpText = "Test the speed of the recovery", Default = false)]
        public bool Benchmark { get; set; }

        [Option(shortName: 'c', longName: "connection", Required = false, HelpText = "Connection string for KNX GW or USB device to use")]
        public String ConnectionString { get; set; }

        [Option(shortName: 't', longName: "target", Required = false, HelpText = "Address of the target")]
        public String KNXTarget { get; set; }

        [Option(shortName: 'k', longName: "keys", Required = false, HelpText = "Key file for dictionary attack", Default = "keys.txt")]
        public String Keyfile { get; set; }

        [Option(shortName: 'm', longName: "maxnumber", Required = false, HelpText = "Maximal number of bruteforces", Default = 1)]
        public int MaxWorkes { get; set; }

        [Option(shortName: 'i', longName: "worker", Required = false, HelpText = "Number of the bruteforcer", Default = 1)]
        public int NumberWorker { get; set; }

        [Option(shortName: 's', longName: "seed", Required = false, HelpText = "Seed to be used to iterate the whole key space", Default = 42)]
        public int seed { get; set; }

        [Option(shortName: 'p', longName: "discover", Required = false, HelpText = "Discover a knx device in programming mode", Default = false)]
        public bool DiscoverKNXDevice { get; set; }
    }
}
class KNXBruteForcer
{

    private String DeviceInfo { get; set; }
    private KeySpaceIterator rng { set; get; }
    public static byte PROP_ID_SERIAL = 11;
    public static uint PROGRESS_NONE = uint.MinValue;
    public static uint PROGRESS_DONE = uint.MaxValue;
    public static ProgressBarOptions PROGRESS_OPTIONS = new ProgressBarOptions
    {
        ProgressCharacter = '─',
        ShowEstimatedDuration = true
    };

    public KNXBruteForcer(uint seed)
    {
        this.rng = new KeySpaceIterator(seed);
    }

    void testDevice(Device device)
    {
        Console.WriteLine("Test if device is locked");
        tryKey(device, uint.MaxValue);
        Console.WriteLine("Device is locked");

        var serial = device.ReadProperty(0, PROP_ID_SERIAL, device.ReadPropertyDescriptionById(0, PROP_ID_SERIAL).PropertyDataType, 1, 1);
        DeviceInfo = $"{device.Address}_{BitConverter.ToString(serial)}_{this.rng.seed}";
        Console.WriteLine($"Device info is {DeviceInfo}");
    }
    void tryKey(Device device, uint key)
    {
        while (true)
        {
            try
            {
                byte level = device.Authorize(key);
                if (level != 3 && level != 15)
                    throw new KNXKeyFoundException(key, level);
                return;
            }
            catch (NoResponseReceivedException ex)
            {
                Console.WriteLine(ex.ToString());
                Thread.Sleep(1000);
            }
	    catch (KNXKeyFoundException e)
	    {
	    	Console.WriteLine($"Key Found!\nThe Key for level {e.Level + 1} is {e.Key.ToString("x2")}!");
		System.Environment.Exit(0); // Graceful exit of program
	    }
        }
    }

    private string progressFileFromLevel(uint level)
    {
        if (DeviceInfo == null)
            throw new Exception("Can not use device info before device info has been set");
        return $"{DeviceInfo}_level{level}.txt";
    }

    private uint readProgress(uint level)
    {
        var file = progressFileFromLevel(level);
        if (System.IO.File.Exists(file))
        {
            foreach (var line in System.IO.File.ReadLines(file))
            {
                return uint.Parse(line, System.Globalization.NumberStyles.HexNumber);
            }
        }

        return PROGRESS_NONE;
    }

    private Tuple<uint, uint> readProgressAndSeed(uint level)
    {
        var file = progressFileFromLevel(level);
        if (System.IO.File.Exists(file))
        {

            var lines = System.IO.File.ReadLines(file).ToArray();
            if (lines.Length != 2)
            {
                Console.WriteLine($"File {file} does not contains two lines");
                return null;
            }
            return new Tuple<uint, uint>(uint.Parse(lines[0], System.Globalization.NumberStyles.HexNumber), uint.Parse(lines[0], System.Globalization.NumberStyles.HexNumber));
        }

        return null;
    }

    private void saveProgress(uint level, uint value)
    {
        System.IO.File.WriteAllLines(progressFileFromLevel(level), new string[] { value.ToString("x2") });
        if (value == PROGRESS_DONE)
            Console.WriteLine($"Stage {level}: Done. Tried all keys");
    }

    private void saveProgressAndSeed(uint level, uint value, uint seed)
    {
        System.IO.File.WriteAllLines(progressFileFromLevel(level), new string[] { value.ToString("x2"), seed.ToString("x2") });
        if (value == PROGRESS_DONE)
            Console.WriteLine($"Stage {level}: Done. Tried all keys");
    }
    // from https://stackoverflow.com/questions/223832/check-a-string-to-see-if-all-characters-are-hexadecimal-values
    private bool IsHex(String chars)
    {
        bool isHex; 
        foreach(var c in chars)
        {
            isHex = ((c >= '0' && c <= '9') || 
                    (c >= 'a' && c <= 'f') || 
                    (c >= 'A' && c <= 'F'));

            if(!isHex)
                return false;
        }
        return true;
    }

    private void generateKeyForHacked(){
        Console.Write("Please enter the four middle hex digits of your key:");
        var key_middle_part = Console.ReadLine();
        // test if its hex and has the correct length
        key_middle_part = key_middle_part.ToUpper();
        if(key_middle_part.Count() != 4){
            Console.WriteLine("Middle key does need to be four chars long!");
            return;
        }

        if(!IsHex(key_middle_part)){
            Console.WriteLine("Middle key does not seem to be hex!");
            return;
        }

        string filePath = "keys.txt";

        StringBuilder csvContent = new StringBuilder();

        for (int i = 0; i <= 0xFFFF; i++)
        {            
           string hexValue = i.ToString("X4");
           csvContent.AppendLine($"{hexValue.Substring(0, 1)}{hexValue.Substring(1, 1)}{key_middle_part}{hexValue.Substring(2, 1)}{hexValue.Substring(3, 1)}");
        }
        
        File.WriteAllText(filePath, csvContent.ToString());

        Console.WriteLine($"Key file {filePath} was created!");

    }

    private void benchmark(Device device)
    {
        var max_keys = 200;
        Console.WriteLine($"Trying {max_keys} keys");
        Stopwatch watch = new Stopwatch();
        watch.Start();
        using (var pbar = new ProgressBar(max_keys / 10, "Testing speed", PROGRESS_OPTIONS))
        {
            for (uint i = 0; i < max_keys; i++)
            {
                tryKey(device, 0x42424242);
                if (i % 10 == 0)
                    pbar.Tick($"Tried {i} keys");
            }
        }
        watch.Stop();
        Console.WriteLine($"Took {watch.ElapsedMilliseconds / 1000.0} seconds");
        Console.WriteLine($"Time for one key try: {watch.ElapsedMilliseconds / 1000.0 / max_keys} seconds");
        Console.WriteLine($"Tries per seconds {((float)max_keys / watch.ElapsedMilliseconds / 1000.0)}");
    }

    private void level3(Device device, int maxWorkers, int numberWorker)
    {
        using (var pbar = new ProgressBar((int)(UInt32.MaxValue / 100), "Stage 3: Trying some keys", PROGRESS_OPTIONS))
        {
            var state = readProgressAndSeed(3);
            uint skip = 0;
            if (state != null)
            {
                rng.seed = state.Item2;
                skip = state.Item1;
            }
            for (uint i = 0; i <= uint.MaxValue; i++)
            {
                var key = rng.Next();
                if (i % 100 == 0)
                    pbar.Tick($"Stage 3: Trying all keys: Tried {i} keys");

                if (i < skip)
                    continue;

                if (maxWorkers > 1 && i % maxWorkers != numberWorker)
                    continue;

                tryKey(device, key);

                if (i % 10 == 0 && key > 0)
                    saveProgressAndSeed(3, i, rng.seed);

            }
        }
        saveProgressAndSeed(3, uint.MaxValue, rng.seed);
    }


    private void level2(Device device, String keyfile, int maxWorkers, int numberWorker)
    {
        int lines = System.IO.File.ReadAllLines(keyfile).Length;
        uint skip = readProgress(2);
        uint i = 0;
        using (var pbar = new ProgressBar(lines / 10, "Stage 2: Trying dictionary keys", PROGRESS_OPTIONS))
        {
            foreach (string line in System.IO.File.ReadLines(keyfile))
            {
                i++;
                if (i % 10 == 0)
                    pbar.Tick($"Stage 2: Trying dictionary keys: Tried {i} keys");
                if (i <= skip)
                    continue;

                if (maxWorkers > 1 && i % maxWorkers != numberWorker)
                    continue;

                tryKey(device, uint.Parse(line, System.Globalization.NumberStyles.HexNumber));

                if (i % 100 == 0 && i > 0)
                    saveProgress(2, i);
            }
            saveProgress(2, i);
        }
    }

    void level1(Device device)
    {
        Console.WriteLine("Stage 1: Trying default keys");
        if (readProgress(1) == PROGRESS_DONE)
        {
            Console.WriteLine("Already tried stage 1");
            return;
        }
        // keys are based on some well known default keys and keys from real attacks
        var key_space = new UInt32[] { 0x11223344, 0x12345678, 0x00000000, 0x87654321, 0x11111111, 0xffffffff, 0x42424242, 0x1235468, 0x24155165, 0x12354789, 0x47566566, 0x26516886, 0xC };
        foreach (var key in key_space)
        {
            tryKey(device, key);
        }

        saveProgress(1, PROGRESS_DONE);
    }
    static void Main(string[] args)
    {
        var parserResult = Parser.Default.ParseArguments<CommandLineOptions>(args)
               .WithParsed<CommandLineOptions>(o =>
               {
                   KNXBruteForcer brute = new KNXBruteForcer((uint)o.seed);
                   if(o.GenerateKeysFromMiddleKey){
                        brute.generateKeyForHacked();
                        return;
                   }
                   else if (o.ListUsb)
                   {
                       brute.PrintUsbDevices();
                       return;
                   }
                   else if (o.ListNetwork)
                   {
                       brute.NetworkDevices();
                       return;
                   }
                   else if(o.DiscoverKNXDevice){
                        brute.DiscoverDevice(o);
                   }
                   else
                   {
                       if (o.MaxWorkes > 1 && o.NumberWorker > o.MaxWorkes)
                       {
                           Console.WriteLine("Worker number must be smaller than maxworker (try --help for more information)");
                           return;
                       }

                       if (!o.TryAllKeys && !o.TryDefaultKeys && !o.TryDictionaryKeys)
                       {
                           o.TryDefaultKeys = true;  // try all methods as default
                           o.TryAllKeys = true;
                           o.TryDictionaryKeys = true;
                       }

                       brute.BruteForce(o);
                   }

               });
    }


    private void DiscoverDevice(CommandLineOptions o)
    {
        ConnectorParameters conParams = null;
        if (o.ConnectionString != null)
        {
            conParams = ConnectorParameters.FromConnectionString(o.ConnectionString);
        }

        if (conParams == null)
        {
            Console.WriteLine("You must set a connection string either for a usb device or a KNX GW");
            return;
        }

        using (Bus sut = new Bus(conParams))
        {
            Console.WriteLine("Connecting to KNX bus");
            sut.Connect();
        }

        Console.WriteLine("KNX Device is not implemented yet!");

    }

    private void BruteForce(CommandLineOptions o)
    {
        var target = o.KNXTarget;
        if (target == null)
        {
            Console.WriteLine("KNX Target address not set!");
            return;
        }
        ConnectorParameters conParams = null;
        if (o.ConnectionString != null)
        {
            conParams = ConnectorParameters.FromConnectionString(o.ConnectionString);
        }

        if (conParams == null)
        {
            Console.WriteLine("You must set a connection string either for a usb device or a KNX GW");
            return;
        }

        using (Bus sut = new Bus(conParams))
        {
            Console.WriteLine("Connecting to KNX bus");
            sut.Connect();
            Console.WriteLine("Test if device can be pinged");
            if (!sut.Network.PingIndividualAddress(target, false))
            {
                Console.WriteLine($"Could not connect to {target}");
                return;
            }

            using (Device device = sut.OpenDevice(target))
            {
                testDevice(device);
                if (o.Benchmark)
                {
                    benchmark(device);
                }
                else
                {
                    if (o.TryDefaultKeys)
                        level1(device);
                    if (o.TryDictionaryKeys)
                        level2(device, o.Keyfile, o.MaxWorkes, o.NumberWorker);
                    if (o.TryAllKeys)
                        level3(device, o.MaxWorkes, o.NumberWorker);
                }
            }
        }
    }

    private void PrintEnumeratedDevices(ConnectorParameters[] devices)
    {
        Console.WriteLine($"Found {devices.Length} devices");
        foreach (var device in devices)
        {
            Console.WriteLine(device.ToConnectionString());
        }
    }

    private void PrintUsbDevices()
    {
        Console.WriteLine("Enumerating USB devices");
        PrintEnumeratedDevices(UsbDeviceEnumerator.GetAvailableInterfaces());
    }


    private void NetworkDevices()
    {
        DiscoveryClient discoveryClient = new DiscoveryClient(AdapterTypes.All);
        Console.WriteLine("Discovering network devices for 10 seconds");
        var devices = discoveryClient.Discover(TimeSpan.FromSeconds(10));
        Console.WriteLine($"Found {devices.Length} devices");
        foreach (var device in devices)
        {
            Console.WriteLine(new KnxIpTunnelingConnectorParameters(device).ToString());
        }
    }
}


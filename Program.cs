using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using CommandLine;
using CommandLine.Text;
using Knx.Bus.Common;
using Knx.Bus.Common.Configuration;
using Knx.Bus.Common.Exceptions;
using Knx.Bus.Common.KnxIp;
using Knx.Falcon;
using Knx.Falcon.Sdk;
using ShellProgressBar;
using knxunlock;

namespace knxunlock
{

    public class KNXKeyFoundException : Exception
    {
        public KNXKeyFoundException()
        {
        }

        public KNXKeyFoundException(uint key, uint level)
            : base($"Found key {key.ToString("x2")} for level {level}")
        {
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

        [Option(shortName: 'N', longName: "networklist", Required = false, HelpText = "List all the discoverable KNX IP Gatways", Default = false)]
        public bool ListNetwork { get; set; }

        [Option(shortName: '1', longName: "defaultkeys", Required = false, HelpText = "Try the default keys", Default = false)]
        public bool TryDefaultKeys { get; set; }

        [Option(shortName: '2', longName: "dictionaryKeys", Required = false, HelpText = "Try the dict keys", Default = false)]
        public bool TryDictionaryKeys { get; set; }

        [Option(shortName: '3', longName: "allkeys", Required = false, HelpText = "Try the all keys", Default = false)]
        public bool TryAllKeys { get; set; }

        [Option(shortName: 'b', longName: "benchmark", Required = false, HelpText = "Test the speed of the recovery", Default = false)]
        public bool Benchmark { get; set; }

        [Option(shortName: 'c', longName: "usb", Required = false, HelpText = "Connectionstring for the USB device or KNX GW to use")]
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

    private void level4(Device device, int maxWorkers, int numberWorker)
    {
        using (var pbar = new ProgressBar((int)(uint.MaxValue / 10), "Stage 4: Trying all keys", PROGRESS_OPTIONS))
        {
            uint skip = readProgress(4);
            for (uint i = 0; i <= uint.MaxValue; i++)
            {
                var key = rng.Next();
                if (i % 10 == 0)
                    pbar.Tick($"Stage 4: Trying all keys: Tried {i} keys");

                if (i <= skip)
                    continue;

                if (maxWorkers > 1 && i % maxWorkers != numberWorker)
                    continue;

                Console.WriteLine(key.ToString("x2"));
                tryKey(device, key);
                if (i % 100 == 0 && key > 0)
                    saveProgress(4, key);
            }
            saveProgress(4, uint.MaxValue);
        }
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
                    pbar.Tick($"Stage 3: Trying some keys: Tried {i} keys");

                if (i < skip)
                    continue;

                if (maxWorkers > 1 && i % maxWorkers != numberWorker)
                    continue;

                if (!key.ToString("x2").All(char.IsDigit))
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
        // keys are based on same well known default keys and keys from real attacks
        var key_space = new UInt32[] { 0x11223344, 0x12345678, 0x00000000, 0x87654321, 0x11111111, 0xffffffff, 0x42424242, 0x1235468, 0x24155165, 0x12354789, 0x47566566, 0x26516886, 0xC};
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
                   if (o.ListUsb)
                   {
                       brute.PrintUsbDevices();
                       return;
                   }
                   else if (o.ListNetwork)
                   {
                       brute.NetworkDevices();
                       return;
                   }
                   else
                   {
                       if (o.MaxWorkes > 1 && o.NumberWorker > o.MaxWorkes)
                       {
                           Console.WriteLine("Worker number must be smaller maxworker (try --help for more information)");
                           return;
                       }

                   if (!o.TryAllKeys && !o.TryDefaultKeys && !o.TryDictionaryKeys)
                   {
                       Console.WriteLine("You should at least specify one burteforce option (try --help for more information)");
                       return;
                   }

                   brute.BruteForce(o);
                   }

               });     
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
            Console.WriteLine("You must set a connectionstring either for a usb device or a KNX GW");
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
                    if(o.TryDefaultKeys)
                        level1(device);
                    if(o.TryDictionaryKeys)                    
                        level2(device, o.Keyfile, o.MaxWorkes, o.NumberWorker);
                    if(o.TryAllKeys)
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


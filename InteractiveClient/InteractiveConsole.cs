using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace InteractiveClient
{
    internal class InteractiveConsole
    {
        public bool IsActive { get; private set; } = true;
        private Client _client = new Client();
        private ConsumerController _consumerController = null;
        private long _repeatWaitStopRequested = 0;

        public void Start()
        {
            Console.WriteLine("\n\n--------------------------");
            Console.WriteLine("Client Interactive Console");
            Console.WriteLine("--------------------------\n\n");

            while (IsActive)
            {
                if (_client != null && _client.IsActive)
                {
                    Console.WriteLine();
                    Console.Write(_client.Endpoint.Address + ":" + _client.Endpoint.Port + " :: ");
                }
                else
                {
                    Console.WriteLine();
                    Console.Write("CLIENT :: ");
                }

                var userInput = Console.ReadLine();
                if (userInput.Trim() == "") continue;

                ProcessCommand(userInput);
            }
        }

        private void ProcessCommand(string userInput)
        {
            Command command;
            if (Command.TryParse(out command, userInput))
            {
                if (!_client.IsActive || command.IsLocal)
                {
                    Execute(command);
                }
                else
                {
                    var messenger = new Messenger();
                    _client.Send(userInput, messenger);
                    if (messenger.HasMessage) ShowMessage(messenger);

                    if (messenger.IsError) return;

                    messenger.ClearMessage();
                    _client.Receive(messenger);
                    if (messenger.HasMessage) ShowMessage(messenger);
                }
            }
            else
            {
                ShowInvalidCommand(userInput);
            }
        }

        private void Execute(Command command)
        {
            switch (command.Action)
            {
                case CommandTypes.CONNECT:
                    InitializeClient(command);
                    break;
                case CommandTypes.REPEAT:
                    RepeatCommand(command);
                    break;
                case CommandTypes.REPEAT_WAIT:
                    RepeatWait(command);
                    break;
                case CommandTypes.START_CONSUMERS:
                    StartConsumers(command);
                    break;
                case CommandTypes.STOP_CONSUMERS:
                    StopConsumers(command);
                    break;
                case CommandTypes.STOP_ALL_CONSUMERS:
                    StopAllConsumers();
                    break;
                case CommandTypes.LIST_WORDS:
                    ListWords();
                    break;
                case CommandTypes.LIST_ALL_WORDS:
                    ListAllWords();
                    break;
                case CommandTypes.SAVE_WORDS:
                    SaveWords();
                    break;
                case CommandTypes.CLEAR:
                    Console.Clear();
                    break;
                case CommandTypes.EXIT:
                    Exit();
                    break;
                default:
                    ShowInvalidCommand(command.ToString());
                    break;
            }
        }

        private void RepeatCommand(Command command)
        {
            if (command.Arguments.Length < 2)
            {
                Console.WriteLine("\nREPEAT: INVALID NUMBER OF ARGUMENTS\n");
            }

            int repeatCount;
            if (!int.TryParse(command.Arguments[0], out repeatCount))
            {
                Console.WriteLine("\nREPEAT: INVALID REPEAT COUNT\n");
            }

            for (int i=0; i<repeatCount; i++)
            {
                ProcessCommand(String.Join(" ", command.Arguments.Skip(1)));
            }
        }

        private void Exit()
        {
            if (_client != null)
            {
                Console.WriteLine("\nDISCONNECTING...\n");
                StopAllConsumers();
                _client.Kill();
            }

            Console.WriteLine("\nEXITING\n");
            IsActive = false;
        }

        private void ShowMessage(Messenger messenger)
        {
            if (messenger.IsError) Console.WriteLine("\nERROR");

            if (messenger.IsFormatted)
                ShowFormattedMessage(messenger.Message.ToString());
            else
                Console.WriteLine(messenger.Message.ToString());
        }

        private void ShowFormattedMessage(string message)
        {
            var dataSplit = message.Split("\n", StringSplitOptions.RemoveEmptyEntries);

            if (dataSplit.Length < 4)
            {
                Console.WriteLine("INVALID FORMATTED MESSAGE");
                return;
            }

            var format = dataSplit[1];
            var totalWidth = 
                format.Split(new char[] { '{', '}' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(f => int.Parse(f.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries)[1]))
                .Sum();

            var rowStart = 2;

            if (dataSplit[0].Contains("<COLUMNS>"))
            {
                var columns = dataSplit[2].Split(",");
                rowStart++;

                Console.WriteLine(format, columns);
            }

            Console.WriteLine(new string('-', totalWidth));

            foreach (var row in dataSplit.Skip(rowStart))
            {
                Console.WriteLine(format, row.Split(','));
            }
        }

        private void ShowInvalidCommand(string attemptedCommand)
        {
            Console.WriteLine($"\nINVALID COMMAND: { attemptedCommand }\n");
        }

        private void InitializeClient(Command command)
        {
            string hostName = null;
            int port;

            if (command.Arguments.Length == 1)
            {
                var split = command.Arguments[0].Split(":");
                hostName = split[0];
                if (split.Length == 2 && int.TryParse(split[1], out port))
                {
                    ConnectClient(hostName, port);
                    return;
                }
            }

            if (command.Arguments.Length == 2 && int.TryParse(command.Arguments[1], out port))
            {
                ConnectClient(command.Arguments[0], port);
                return;
            }

            Console.WriteLine("INVALID CONNECT ARGUMENTS: " + String.Join(" ", command.Arguments));
        }

        private void ConnectClient(string hostName, int port)
        {
            var messenger = new Messenger();
            _client.Connect(hostName, port, messenger);
            if (messenger.HasMessage) ShowMessage(messenger);
        }

        private void StartConsumers(Command command)
        {
            int consumerCount = 0;
            if (command.Arguments.Length > 0 && !int.TryParse(command.Arguments[0], out consumerCount))
            {
                Console.WriteLine("INVALID CONSUMER COUNT");
                return;
            }

            if (_consumerController == null )
            {
                _consumerController = new ConsumerController(_client);
            }

            if (consumerCount == 0)
            {
                _consumerController.StartSelfAdjustingConsumers();
            }
            else
            {
                _consumerController.StartConsumers(consumerCount);
            }

            if (consumerCount == 0)
            {
                Console.WriteLine("SELF-ADJUSTING CONSUMERS STARTED");
            }
            else
            {
                Console.WriteLine($"{consumerCount} CONSUMERS STARTED | {_consumerController.ConsumerCount} CONSUMERS RUNNING");
            }
        }

        private void StopConsumers(Command command)
        {
            int consumerCount;
            if (command.Arguments.Length < 1 || !int.TryParse(command.Arguments[0], out consumerCount))
            {
                Console.WriteLine("INVALID CONSUMER COUNT");
                return;
            }

            if (_consumerController == null)
            {
                Console.WriteLine("NO CONSUMER CONTROLLER IS RUNNING");
            }

            _consumerController.StopConsumers(consumerCount);
        }

        private void StopAllConsumers()
        {
            if (_consumerController == null) return;

            _consumerController.StopAllConsumers();
        }

        private void ListWords()
        {
            if (_consumerController == null)
            {
                Console.WriteLine("NO CONSUMER CONTROLLER IS RUNNING");
            }

            var orderedWordList = _consumerController.Words.OrderBy(w => w.Index).ToList();
            Console.WriteLine(String.Join(" ", orderedWordList.Select(w => w.Text)));
        }

        private void ListAllWords()
        {
            if (_consumerController == null)
            {
                Console.WriteLine("NO CONSUMER CONTROLLER IS RUNNING");
            }

            Console.WriteLine(String.Join(" ", _consumerController.Words.Select(w => w.Text)));
        }

        private void SaveWords()
        {
            if (_consumerController == null)
            {
                Console.WriteLine("NO CONSUMER CONTROLLER IS RUNNING");
            }

            var orderedWordList = _consumerController.Words.OrderBy(w => w.Index).ToList();
            var wordText = String.Join(" ", orderedWordList.Select(w => w.Text));

            var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
            var fileName = Path.Combine(dir.FullName, "MobyDick.txt");
            File.WriteAllText(fileName, wordText);

            Console.WriteLine($"WORDS SAVED TO: {fileName}");
        }

        private void RepeatWait(Command command)
        {
            if (command.Arguments.Length < 2)
            {
                Console.WriteLine("\nREPEAT-WAIT: INVALID NUMBER OF ARGUMENTS\n");
                return;
            }

            int waitMilli;
            if (!int.TryParse(command.Arguments[0], out waitMilli))
            {
                Console.WriteLine("\nREPEAT-WAIT: INVALID MILLISECOND WAIT-TIME\n");
                return;
            }

            _repeatWaitStopRequested = 0;

            var thread = new Thread(new ThreadStart(() => WaitForEscape()));
            thread.Start();

            while (true)
            {
                if (Interlocked.Read(ref _repeatWaitStopRequested) == 1)
                {
                    break;
                }

                Console.WriteLine();
                ProcessCommand(String.Join(" ", command.Arguments.Skip(1)));
                Thread.Sleep(waitMilli);
            }

            thread.Join();
        }

        private void WaitForEscape()
        {
            while (Console.ReadKey(true).Key != ConsoleKey.Escape) {}

            Interlocked.Exchange(ref _repeatWaitStopRequested, 1);
        }
    }
}

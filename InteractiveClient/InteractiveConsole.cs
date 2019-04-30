using System;
using System.Collections.Generic;
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

                    messenger.Clear();
                    _client.Receive(messenger);
                    if (messenger.HasMessage) ShowMessage(messenger);

                    if (command.Action == "disconnect")
                    {
                        Disconnect();
                    }
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
                case CommandTypes.EXIT:
                    Disconnect();
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

        private void Disconnect()
        {
            if (_client != null && _client.IsActive)
            {
                Console.WriteLine("\nDISCONNECTING...\n");
                var messenger = new Messenger();
                _client.Kill(messenger);
                if (messenger.HasMessage) ShowMessage(messenger);
            }
        }

        private void Exit()
        {
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
    }
}

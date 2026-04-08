using UnityEngine;
using DaggerfallWorkshop.Game;
using DaggerfallWorkshop.Game.Utility.ModSupport;
using DaggerfallWorkshop.Game.Utility.ModSupport.ModSettings;
using Wenzil.Console;

namespace ConsoleCommandBindsMod
{
    public class ConsoleCommandBinds : MonoBehaviour
    {
        private static Mod mod;

        [Invoke(StateManager.StateTypes.Start, 0)]
        public static void Init(InitParams initParams)
        {
            mod = initParams.Mod;

            var go = new GameObject(mod.Title);
            go.AddComponent<ConsoleCommandBinds>();

            mod.IsReady = true;
        }

        string[] commands1;
        KeyCode[] keyCodes1;
        bool executed1;

        string[] commands2;
        KeyCode[] keyCodes2;
        bool executed2;

        string[] commands3;
        KeyCode[] keyCodes3;
        bool executed3;

        string[] commands4;
        KeyCode[] keyCodes4;
        bool executed4;

        string[] commands5;
        KeyCode[] keyCodes5;
        bool executed5;

        string[] commands6;
        KeyCode[] keyCodes6;
        bool executed6;

        string[] commands7;
        KeyCode[] keyCodes7;
        bool executed7;

        string[] commands8;
        KeyCode[] keyCodes8;
        bool executed8;

        string[] commands9;
        KeyCode[] keyCodes9;
        bool executed9;

        string[] commands10;
        KeyCode[] keyCodes10;
        bool executed10;

        private void Start()
        {
            mod.LoadSettingsCallback = LoadSettings;
            mod.LoadSettings();
        }

        private void Update()
        {
            if (commands1 != null && commands1.Length > 0)
            {
                if (!CheckCommand(commands1, keyCodes1, ref executed1) && executed1)
                    executed1 = false;
            }
            if (commands2 != null && commands2.Length > 0)
            {
                if (!CheckCommand(commands2, keyCodes2, ref executed2) && executed2)
                    executed2 = false;
            }
            if (commands3 != null && commands3.Length > 0)
            {
                if (!CheckCommand(commands3, keyCodes3, ref executed3) && executed3)
                    executed3 = false;
            }
            if (commands4 != null && commands4.Length > 0)
            {
                if (!CheckCommand(commands4, keyCodes4, ref executed4) && executed4)
                    executed4 = false;
            }
            if (commands5 != null && commands5.Length > 0)
            {
                if (!CheckCommand(commands5, keyCodes5, ref executed5) && executed5)
                    executed5 = false;
            }
            if (commands6 != null && commands6.Length > 0)
            {
                if (!CheckCommand(commands6, keyCodes6, ref executed6) && executed6)
                    executed6 = false;
            }
            if (commands7 != null && commands7.Length > 0)
            {
                if (!CheckCommand(commands7, keyCodes7, ref executed7) && executed7)
                    executed7 = false;
            }
            if (commands8 != null && commands8.Length > 0)
            {
                if (!CheckCommand(commands8, keyCodes8, ref executed8) && executed8)
                    executed8 = false;
            }
            if (commands9 != null && commands9.Length > 0)
            {
                if (!CheckCommand(commands9, keyCodes9, ref executed9) && executed9)
                    executed9 = false;
            }
            if (commands10 != null && commands10.Length > 0)
            {
                if (!CheckCommand(commands10, keyCodes10, ref executed10) && executed10)
                    executed10 = false;
            }

            //no commands are being held down, allow new commands on next frame
            //executed = false;
        }

        bool CheckCommand(string[] commands, KeyCode[] keyCodes, ref bool executed)
        {
            bool pressed = true;

            foreach (KeyCode keyCode in keyCodes)
            {
                if (!InputManager.Instance.GetKey(keyCode))
                {
                    pressed = false;
                    break;
                }
            }

            if (pressed && !executed)
            {
                foreach (string command in commands)
                {
                    ExecuteCommand(command);
                }
                executed = true;
            }

            return pressed;
        }

        private void LoadSettings(ModSettings settings, ModSettingsChange change)
        {
            if (change.HasChanged("Command1"))
            {
                commands1 = GetCommands(settings.GetValue<string>("Command1", "Commands"));
                keyCodes1 = GetKeyCodes(settings.GetValue<string>("Command1", "KeyCodes"));
            }
            if (change.HasChanged("Command2"))
            {
                commands2 = GetCommands(settings.GetValue<string>("Command2", "Commands"));
                keyCodes2 = GetKeyCodes(settings.GetValue<string>("Command2", "KeyCodes"));
            }
            if (change.HasChanged("Command3"))
            {
                commands3 = GetCommands(settings.GetValue<string>("Command3", "Commands"));
                keyCodes3 = GetKeyCodes(settings.GetValue<string>("Command3", "KeyCodes"));
            }
            if (change.HasChanged("Command4"))
            {
                commands4 = GetCommands(settings.GetValue<string>("Command4", "Commands"));
                keyCodes4 = GetKeyCodes(settings.GetValue<string>("Command4", "KeyCodes"));
            }
            if (change.HasChanged("Command5"))
            {
                commands5 = GetCommands(settings.GetValue<string>("Command5", "Commands"));
                keyCodes5 = GetKeyCodes(settings.GetValue<string>("Command5", "KeyCodes"));
            }
            if (change.HasChanged("Command6"))
            {
                commands6 = GetCommands(settings.GetValue<string>("Command6", "Commands"));
                keyCodes6 = GetKeyCodes(settings.GetValue<string>("Command6", "KeyCodes"));
            }
            if (change.HasChanged("Command7"))
            {
                commands7 = GetCommands(settings.GetValue<string>("Command7", "Commands"));
                keyCodes7 = GetKeyCodes(settings.GetValue<string>("Command7", "KeyCodes"));
            }
            if (change.HasChanged("Command8"))
            {
                commands8 = GetCommands(settings.GetValue<string>("Command8", "Commands"));
                keyCodes8 = GetKeyCodes(settings.GetValue<string>("Command8", "KeyCodes"));
            }
            if (change.HasChanged("Command9"))
            {
                commands9 = GetCommands(settings.GetValue<string>("Command9", "Commands"));
                keyCodes9 = GetKeyCodes(settings.GetValue<string>("Command9", "KeyCodes"));
            }
            if (change.HasChanged("Command100"))
            {
                commands10 = GetCommands(settings.GetValue<string>("Command10", "Commands"));
                keyCodes10 = GetKeyCodes(settings.GetValue<string>("Command10", "KeyCodes"));
            }
        }

        string[] GetCommands(string message)
        {
            if (string.IsNullOrEmpty(message) || string.IsNullOrWhiteSpace(message))
                return null;

            string[] commands = message.Split(',');

            for (int i = 0; i < commands.Length; i++)
            {
                commands[i] = commands[i].Trim();
            }

            return commands;
        }

        KeyCode[] GetKeyCodes(string message)
        {
            string[] split = message.Split('+');

            KeyCode[] keyCodes = new KeyCode[split.Length];

            for (int i = 0; i < keyCodes.Length; i++)
            {
                keyCodes[i] = GetKeyCodeFromText(split[i].Trim());
            }

            return keyCodes;
        }

        KeyCode GetKeyCodeFromText(string text)
        {
            Debug.Log("Setting Key");
            if (System.Enum.TryParse(text, out KeyCode result))
            {
                Debug.Log("Key set to " + result.ToString());
                return result;
            }
            else
            {
                Debug.Log("Detected an invalid key code. Setting to default.");
                return KeyCode.None;
            }
        }

        public void ExecuteCommand(string message)
        {
            string[] split = message.Split(' ');

            string command = "";
            string[] args = new string[split.Length - 1];

            for (int i = 0; i < split.Length; i++)
            {
                if (i == 0)
                    command = split[i];
                else
                    args[i - 1] = split[i];
            }

            if (command != "")
            {
                if (args.Length > 0)
                    Console.ExecuteCommand(command, args);
                else
                    Console.ExecuteCommand(command);

                //executed = true;
            }
        }
    }
}

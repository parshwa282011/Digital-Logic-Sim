using System.Collections.Generic;
using Seb.Helpers;
using UnityEngine;

namespace DLS.Simulation
{
	public static class SimKeyboardHelper
	{
		public static readonly KeyCode[] ValidInputKeys =
		{
			KeyCode.Backspace,
			KeyCode.Delete,
			KeyCode.Tab,
			KeyCode.Clear,
			KeyCode.Return,
			KeyCode.Pause,
			KeyCode.Escape,
			KeyCode.Space,
			KeyCode.Keypad0, KeyCode.Keypad1, KeyCode.Keypad2, KeyCode.Keypad3, KeyCode.Keypad4,
			KeyCode.Keypad5, KeyCode.Keypad6, KeyCode.Keypad7, KeyCode.Keypad8, KeyCode.Keypad9,
			KeyCode.KeypadPeriod, KeyCode.KeypadDivide, KeyCode.KeypadMultiply, KeyCode.KeypadMinus,
			KeyCode.KeypadPlus, KeyCode.KeypadEnter, KeyCode.KeypadEquals,
			KeyCode.UpArrow, KeyCode.DownArrow, KeyCode.RightArrow, KeyCode.LeftArrow,
			KeyCode.Insert, KeyCode.Home, KeyCode.End, KeyCode.PageUp, KeyCode.PageDown,
			KeyCode.F1, KeyCode.F2, KeyCode.F3, KeyCode.F4, KeyCode.F5, KeyCode.F6,
			KeyCode.F7, KeyCode.F8, KeyCode.F9, KeyCode.F10, KeyCode.F11, KeyCode.F12,
			KeyCode.F13, KeyCode.F14, KeyCode.F15,
			KeyCode.Alpha0, KeyCode.Alpha1, KeyCode.Alpha2, KeyCode.Alpha3, KeyCode.Alpha4,
			KeyCode.Alpha5, KeyCode.Alpha6, KeyCode.Alpha7, KeyCode.Alpha8, KeyCode.Alpha9,
			KeyCode.Exclaim, KeyCode.DoubleQuote, KeyCode.Hash, KeyCode.Dollar, KeyCode.Percent,
			KeyCode.Ampersand, KeyCode.Quote, KeyCode.LeftParen, KeyCode.RightParen, KeyCode.Asterisk,
			KeyCode.Plus, KeyCode.Comma, KeyCode.Minus, KeyCode.Period, KeyCode.Slash,
			KeyCode.Colon, KeyCode.Semicolon, KeyCode.Less, KeyCode.Equals, KeyCode.Greater,
			KeyCode.Question, KeyCode.At, KeyCode.LeftBracket, KeyCode.Backslash, KeyCode.RightBracket,
			KeyCode.Caret, KeyCode.Underscore, KeyCode.BackQuote,
			KeyCode.A, KeyCode.B, KeyCode.C, KeyCode.D, KeyCode.E, KeyCode.F,
			KeyCode.G, KeyCode.H, KeyCode.I, KeyCode.J, KeyCode.K, KeyCode.L,
			KeyCode.M, KeyCode.N, KeyCode.O, KeyCode.P, KeyCode.Q, KeyCode.R,
			KeyCode.S, KeyCode.T, KeyCode.U, KeyCode.V, KeyCode.W, KeyCode.X,
			KeyCode.Y, KeyCode.Z,
			KeyCode.LeftCurlyBracket, KeyCode.Pipe, KeyCode.RightCurlyBracket, KeyCode.Tilde,
			KeyCode.Numlock, KeyCode.CapsLock, KeyCode.ScrollLock,
			KeyCode.RightShift, KeyCode.LeftShift, KeyCode.RightControl, KeyCode.LeftControl,
			KeyCode.RightAlt, KeyCode.LeftAlt, KeyCode.LeftMeta, KeyCode.LeftCommand,
			KeyCode.LeftApple, KeyCode.LeftWindows, KeyCode.RightMeta, KeyCode.RightCommand,
			KeyCode.RightApple, KeyCode.RightWindows, KeyCode.AltGr,
			KeyCode.Help, KeyCode.Print, KeyCode.SysReq, KeyCode.Break, KeyCode.Menu,
			KeyCode.WheelUp, KeyCode.WheelDown,
			KeyCode.F16, KeyCode.F17, KeyCode.F18, KeyCode.F19,
			KeyCode.F20, KeyCode.F21, KeyCode.F22, KeyCode.F23, KeyCode.F24,
			KeyCode.Mouse0, KeyCode.Mouse1, KeyCode.Mouse2, KeyCode.Mouse3,
			KeyCode.Mouse4, KeyCode.Mouse5, KeyCode.Mouse6,
			KeyCode.JoystickButton0, KeyCode.JoystickButton1, KeyCode.JoystickButton2, KeyCode.JoystickButton3,
			KeyCode.JoystickButton4, KeyCode.JoystickButton5, KeyCode.JoystickButton6, KeyCode.JoystickButton7,
			KeyCode.JoystickButton8, KeyCode.JoystickButton9, KeyCode.JoystickButton10, KeyCode.JoystickButton11,
			KeyCode.JoystickButton12, KeyCode.JoystickButton13, KeyCode.JoystickButton14, KeyCode.JoystickButton15,
			KeyCode.JoystickButton16, KeyCode.JoystickButton17, KeyCode.JoystickButton18, KeyCode.JoystickButton19
		};

		static readonly HashSet<char> KeyLookup = new();
		static bool HasAnyInput;

		// Call from Main Thread
		public static void RefreshInputState()
		{
			lock (KeyLookup)
			{
				KeyLookup.Clear();
				HasAnyInput = false;

				if (!InputHelper.AnyKeyOrMouseHeldThisFrame) return; // early exit if no key held
				if (InputHelper.CtrlIsHeld || InputHelper.ShiftIsHeld || InputHelper.AltIsHeld) return; // don't trigger key chips if modifier is held

				foreach (KeyCode key in ValidInputKeys)
				{
					if (InputHelper.IsKeyHeld(key))
					{
						char keyChar = char.ToUpper((char)key);
						KeyLookup.Add(keyChar);
						HasAnyInput = true;
					}
				}
			}
		}

		// Call from Sim Thread
		public static bool KeyIsHeld(char key)
		{
			bool isHeld;

			lock (KeyLookup)
			{
				isHeld = HasAnyInput && KeyLookup.Contains(key);
			}

			return isHeld;
		}
	}
}
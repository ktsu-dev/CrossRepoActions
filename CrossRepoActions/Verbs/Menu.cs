// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

namespace ktsu.CrossRepoActions.Verbs;

using System.Diagnostics;
using System.Linq;

using CommandLine;

using DustInTheWind.ConsoleTools.Controls;
using DustInTheWind.ConsoleTools.Controls.Menus;
using DustInTheWind.ConsoleTools.Controls.Menus.MenuItems;

[Verb("Menu", isDefault: true)]
internal class Menu : BaseVerb<Menu>
{
	internal override void Run(Menu options)
	{
		var scrollMenu = new ScrollMenu()
		{
			HorizontalAlignment = HorizontalAlignment.Left,
			ItemsHorizontalAlignment = HorizontalAlignment.Left,
			KeepHighlightingOnClose = true,
		};

		var menuRepeater = new ControlRepeater()
		{
			Control = scrollMenu,
		};

		var menuItems = Program.Verbs
			.Where(verb => verb != GetType())
			.Select(CreateMenuItem)
			.ToArray();

		scrollMenu.AddItems(menuItems);

		while (true)
		{
			menuRepeater.Display();
		}
	}

	private static LabelMenuItem CreateMenuItem(Type verbType)
	{
		var verb = Activator.CreateInstance(verbType) as BaseVerb;
		Debug.Assert(verb != null);
		return new LabelMenuItem()
		{
			Text = verbType.Name,
			Command = verb,
			IsEnabled = true,
		};
	}
}


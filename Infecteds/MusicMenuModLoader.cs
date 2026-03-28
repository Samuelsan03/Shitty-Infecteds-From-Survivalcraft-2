using System;
using System.Collections.Generic;
using Game;

public class MenuMusicModLoader : ModLoader
{
	// Lista de rutas de música (sin extensión) dentro del mod
	private static readonly List<string> ListaMusica = new List<string>
	{
		"Music/Menu Music",
		"Music/Menu Music 2",
	};

	private Game.Random random = new Game.Random(); // Asegúrate de usar System.Random o el Random del juego

	public override void __ModInitialize()
	{
		// Registrar el hook con prioridad por defecto (0)
		ModsManager.RegisterHook("MenuPlayMusic", this);
	}

	public override void MenuPlayMusic(out string contentMusicPath)
	{
		// Seleccionar una canción aleatoria de la lista
		int index = random.Int(ListaMusica.Count);
		contentMusicPath = ListaMusica[index];
	}
}

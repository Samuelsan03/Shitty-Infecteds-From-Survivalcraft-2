using Game;

public class MusicMenuModLoader : ModLoader
{
	public override void __ModInitialize()
	{
		// Register our mod loader for the "MenuPlayMusic" hook.
		// Priority: lower values execute first. We want our music to be used if no other mod overrides it,
		// but we can set a default priority. Other mods may also set their own music; we don't want to block them,
		// so we'll use a neutral priority.
		ModsManager.RegisterHook("MenuPlayMusic", this, 0);
	}

	public override void MenuPlayMusic(out string contentMusicPath)
	{
		// Provide the path to our custom menu music.
		// The file should be placed in the mod's content folder under "Music/Menu Music".
		// The game's ContentManager will resolve the path relative to the mod's content.
		contentMusicPath = "Music/Menu Music";
	}
}

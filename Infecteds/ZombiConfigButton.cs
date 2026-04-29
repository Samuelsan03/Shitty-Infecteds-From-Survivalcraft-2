using Game;
using Engine;

public class ZombiConfigButton : BevelledButtonWidget
{
	public override void Update()
	{
		base.Update();               // necesario para que IsClicked funcione
		if (this.IsClicked)
		{
			ScreensManager.SwitchScreen(new ShittyInfectedsSettingsScreen());
		}
	}
}

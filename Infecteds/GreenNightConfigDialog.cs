using System;
using System.Xml.Linq;
using Engine;

namespace Game
{
	public class GreenNightConfigDialog : Dialog
	{
		private ComponentPlayer m_player;
		private SubsystemGreenNightSky m_greenNightSky;

		private int[] m_dayOptions = new int[] { 4, 8, 12, 16 };
		private string[] m_descriptions = new string[]
		{
			"El tiempo vuela cuando te divertes, ¿verdad? Bueno, aquí también vuela, pero no porque te estés divirtiendo.\nLos infectados se multiplicarán más rápido y la oscuridad será más densa.\nAl menos tendrás tiempo de cavar tu tumba con calma.",
			"Casi parece que te toman en serio. Puedes terminar esa pared que dejaste a medias... o no.\nProbablemente no. Al menos cuando mueras, será en una casa a medio construir.\nEso le da carácter.",
			"Relativa calma. Suficiente para que te ilusiones con sobrevivir.\nTe recomiendo no hacer planes a largo plazo, como aprender a tejer o cultivar amistades.\nLos infectados no respetan los hobbies.",
			"Prácticamente unas vacaciones. Tanto tiempo que podrías olvidar que hay zombis afuera.\nVeo que prefieres el estrés en dosis pequeñas. Inteligente.\nO cobarde. Las dos cosas suelen ir juntas."
		};

		private int m_currentIndex = 0;

		public int SelectedDays { get; private set; }

		private ButtonWidget m_okButton;
		private ButtonWidget m_cancelButton;
		private ButtonWidget m_daysButton;
		private LabelWidget m_description;

		public GreenNightConfigDialog(ComponentPlayer player)
		{
			m_player = player;
			m_greenNightSky = player?.Project?.FindSubsystem<SubsystemGreenNightSky>();

			XElement node = ContentManager.Get<XElement>("Dialogs/GreenNightConfigDialog");
			this.LoadContents(this, node);

			m_okButton = this.Children.Find<ButtonWidget>("GreenNightConfig.OkButton", true);
			m_cancelButton = this.Children.Find<ButtonWidget>("GreenNightConfig.CancelButton", true);
			m_daysButton = this.Children.Find<ButtonWidget>("GreenNightConfig.DaysButton", true);
			m_description = this.Children.Find<LabelWidget>("GreenNightConfig.Description", true);

			m_currentIndex = 0;
			SelectedDays = m_dayOptions[m_currentIndex];
			UpdateDaysDisplay();
		}

		private void UpdateDaysDisplay()
		{
			if (m_daysButton != null)
			{
				m_daysButton.Text = m_dayOptions[m_currentIndex] + " días";
			}
			if (m_description != null)
			{
				m_description.Text = m_descriptions[m_currentIndex];
			}
			SelectedDays = m_dayOptions[m_currentIndex];
		}

		public override void Update()
		{
			if (m_daysButton.IsClicked)
			{
				m_currentIndex = (m_currentIndex + 1) % m_dayOptions.Length;
				UpdateDaysDisplay();
			}

			if (m_okButton.IsClicked)
			{
				if (m_greenNightSky != null)
				{
					m_greenNightSky.SetGreenNightInterval(SelectedDays);
				}

				if (m_player != null && m_player.ComponentGui != null)
				{
					m_player.ComponentGui.DisplaySmallMessage(
						"La noche verde ocurrirá en " + SelectedDays + " días",
						new Color(0, 255, 94),
						false,
						true
					);
				}
				DialogsManager.HideDialog(this);
			}
			if (m_cancelButton.IsClicked || base.Input.Cancel)
			{
				if (m_greenNightSky != null)
				{
					m_greenNightSky.SetGreenNightInterval(4);
				}

				if (m_player != null && m_player.ComponentGui != null)
				{
					m_player.ComponentGui.DisplaySmallMessage(
						"La noche verde ocurrirá en 4 días",
						new Color(0, 255, 94),
						false,
						true
					);
				}
				DialogsManager.HideDialog(this);
			}
		}
	}
}

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

		private string[] m_difficultyNames = new string[]
		{
			"Sobrevivirás... Quizás",
			"La Muerte te Espera",
			"Tu Tumba ya Está Lista",
			"No Hay Esperanza",
			"Suicidio Asegurado",
			"Ni Dios Te Salva"
		};

		private Color[] m_difficultyColors = new Color[]
		{
			new Color(180, 230, 180),
			new Color(255, 255, 100),
			new Color(255, 165, 0),
			new Color(220, 60, 60),
			new Color(139, 0, 0),
			new Color(40, 0, 40)
		};

		private DifficultyModes[] m_difficulties = new DifficultyModes[]
		{
			DifficultyModes.VeryEasy,
			DifficultyModes.Easy,
			DifficultyModes.Normal,
			DifficultyModes.Medium,
			DifficultyModes.Hard,
			DifficultyModes.Extreme
		};

		private int m_currentIndex = 0;
		private int m_currentDifficultyIndex = 2;

		public int SelectedDays { get; private set; }
		public DifficultyModes SelectedDifficulty { get; private set; }

		private ButtonWidget m_okButton;
		private ButtonWidget m_cancelButton;
		private ButtonWidget m_daysButton;
		private BevelledButtonWidget m_difficultyButton;
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
			m_difficultyButton = this.Children.Find<BevelledButtonWidget>("GreenNightConfig.DifficultyButton", true);
			m_description = this.Children.Find<LabelWidget>("GreenNightConfig.Description", true);

			m_currentIndex = 0;
			SelectedDays = m_dayOptions[m_currentIndex];
			SelectedDifficulty = m_difficulties[m_currentDifficultyIndex];
			UpdateDaysDisplay();
			UpdateDifficultyDisplay();
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

		private void UpdateDifficultyDisplay()
		{
			if (m_difficultyButton != null)
			{
				m_difficultyButton.Text = m_difficultyNames[m_currentDifficultyIndex];
				m_difficultyButton.CenterColor = m_difficultyColors[m_currentDifficultyIndex];
				m_difficultyButton.BevelColor = m_difficultyColors[m_currentDifficultyIndex] * 0.7f;
			}
			SelectedDifficulty = m_difficulties[m_currentDifficultyIndex];
		}

		public override void Update()
		{
			if (m_daysButton.IsClicked)
			{
				m_currentIndex = (m_currentIndex + 1) % m_dayOptions.Length;
				UpdateDaysDisplay();
			}

			if (m_difficultyButton != null && m_difficultyButton.IsClicked)
			{
				m_currentDifficultyIndex = (m_currentDifficultyIndex + 1) % m_difficultyNames.Length;
				UpdateDifficultyDisplay();
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
						"La Noche Verde estará en " + m_difficultyNames[m_currentDifficultyIndex] + " y ocurrirá en " + SelectedDays + " días",
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
						"La Noche Verde estará en Tu Tumba ya Está Lista y ocurrirá en 4 días",
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

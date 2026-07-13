using System;
using System.Xml.Linq;
using Engine;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game
{
	public class GreenNightActivationDialog : Dialog
	{
		private SubsystemGreenNightSky m_subsystemGreenNight;

		private CheckboxWidget m_activateCheckbox;

		private LabelWidget m_descriptionLabel;

		private LabelWidget m_titleLabel;

		private ButtonWidget m_okButton;

		private ButtonWidget m_cancelButton;

		private ButtonWidget m_configButton;

		private string[] m_difficultyNames = new string[]
		{
			"Sobrevivirás... Quizás",
			"La Muerte te Espera",
			"Tu Tumba ya Está Lista",
			"No Hay Esperanza",
			"Suicidio Asegurado",
			"Ni Dios Te Salva"
		};

		public GreenNightActivationDialog(SubsystemGreenNightSky subsystemGreenNight)
		{
			XElement node = ContentManager.Get<XElement>("Dialogs/GreenNightActivationDialog");
			this.LoadContents(this, node);
			this.m_titleLabel = this.Children.Find<LabelWidget>("GreenNightActivationDialog.Title", true);
			this.m_activateCheckbox = this.Children.Find<CheckboxWidget>("GreenNightActivationDialog.ActivateCheckbox", true);
			this.m_descriptionLabel = this.Children.Find<LabelWidget>("GreenNightActivationDialog.Description", true);
			this.m_okButton = this.Children.Find<ButtonWidget>("GreenNightActivationDialog.OkButton", true);
			this.m_cancelButton = this.Children.Find<ButtonWidget>("GreenNightActivationDialog.CancelButton", true);
			this.m_configButton = this.Children.Find<ButtonWidget>("GreenNightActivationDialog.ConfigButton", true);
			this.m_subsystemGreenNight = subsystemGreenNight;

			if (this.m_subsystemGreenNight != null)
			{
				this.m_activateCheckbox.IsChecked = this.m_subsystemGreenNight.IsGreenNightEnabled;
			}

			this.m_titleLabel.Text = LanguageControl.Get("GreenNightActivationDialog", 1);
			this.m_activateCheckbox.Text = LanguageControl.Get("GreenNightActivationDialog", 2);
			this.m_configButton.Text = LanguageControl.Get("GreenNightActivationDialog", 5);
			this.m_okButton.Text = LanguageControl.Get("GreenNightActivationDialog", 7);
			this.m_cancelButton.Text = LanguageControl.Get("GreenNightActivationDialog", 6);
			this.UpdateDescription();
		}

		public override void Update()
		{
			if (this.m_activateCheckbox.IsClicked)
			{
				this.UpdateDescription();
			}
			if (this.m_configButton != null && this.m_configButton.IsClicked)
			{
				ComponentPlayer player = GetPlayer();
				if (player != null)
				{
					DialogsManager.ShowDialog(null, new GreenNightConfigDialog(player, false));
				}
			}
			if (this.m_okButton.IsClicked)
			{
				if (this.m_subsystemGreenNight != null)
				{
					bool newState = this.m_activateCheckbox.IsChecked;
					bool oldState = this.m_subsystemGreenNight.IsGreenNightEnabled;

					if (newState != oldState)
					{
						this.m_subsystemGreenNight.SetGreenNightActive(newState);
					}

					ComponentPlayer player = GetPlayer();
					if (player?.ComponentGui != null && player.ComponentHealth?.Health > 0)
					{
						if (newState)
						{
							int days = this.m_subsystemGreenNight.GreenNightIntervalDays;
							int diffIndex = (int)this.m_subsystemGreenNight.CurrentDifficulty;
							string diffName = m_difficultyNames[Math.Min(diffIndex, m_difficultyNames.Length - 1)];

							player.ComponentGui.DisplaySmallMessage(
								"La Noche Verde estará en " + diffName + " y ocurrirá en " + days + " días",
								new Color(0, 255, 94),
								false,
								true
							);
						}
						else
						{
							player.ComponentGui.DisplaySmallMessage(
								LanguageControl.Get("GreenNightActivationDialog", 9),
								new Color(255, 200, 100),
								false,
								true
							);
						}
					}
				}
				Dismiss();
			}
			if (base.Input.Cancel || this.m_cancelButton.IsClicked)
			{
				Dismiss();
			}
		}

		public void Dismiss()
		{
			DialogsManager.HideDialog(this);
		}

		private void UpdateDescription()
		{
			if (this.m_activateCheckbox.IsChecked)
			{
				this.m_descriptionLabel.Text = LanguageControl.Get("GreenNightActivationDialog", 4);
			}
			else
			{
				this.m_descriptionLabel.Text = LanguageControl.Get("GreenNightActivationDialog", 3);
			}
		}

		private ComponentPlayer GetPlayer()
		{
			if (m_subsystemGreenNight?.Project == null) return null;
			foreach (Entity entity in m_subsystemGreenNight.Project.Entities)
			{
				if (entity != null)
				{
					ComponentPlayer p = entity.FindComponent<ComponentPlayer>();
					if (p != null) return p;
				}
			}
			return null;
		}

		public const string fName = "GreenNightActivationDialog";
	}
}

using System;
using System.Xml.Linq;
using Engine;

namespace Game
{
	public class GreenNightActivationDialog : Dialog
	{
		private SubsystemGreenNightSky m_subsystemGreenNight;

		private CheckboxWidget m_activateCheckbox;

		private LabelWidget m_descriptionLabel;

		private ButtonWidget m_okButton;

		private ButtonWidget m_cancelButton;

		private const string DescriptionOff = "El mundo mantiene su equilibrio habitual\n con rangos de percepción limitados y\n persecuciones breves.";

		private const string DescriptionOn = "El mundo entra en un estado de agresividad\n descontrolada. Las criaturas expanden\n drásticamente su campo de percepción, \ndesatan persecuciones inagotables que no\n ceden ante obstáculos ni pérdida de\n contacto visual,\n priorizando la caza de forma absoluta.";

		public GreenNightActivationDialog(SubsystemGreenNightSky subsystemGreenNight)
		{
			XElement node = ContentManager.Get<XElement>("Dialogs/GreenNightActivationDialog");
			this.LoadContents(this, node);
			this.m_activateCheckbox = this.Children.Find<CheckboxWidget>("GreenNightActivationDialog.ActivateCheckbox", true);
			this.m_descriptionLabel = this.Children.Find<LabelWidget>("GreenNightActivationDialog.Description", true);
			this.m_okButton = this.Children.Find<ButtonWidget>("GreenNightActivationDialog.OkButton", true);
			this.m_cancelButton = this.Children.Find<ButtonWidget>("GreenNightActivationDialog.CancelButton", true);
			this.m_subsystemGreenNight = subsystemGreenNight;

			if (this.m_subsystemGreenNight != null)
			{
				// Ahora lee el estado del interruptor maestro
				this.m_activateCheckbox.IsChecked = this.m_subsystemGreenNight.IsGreenNightEnabled;
			}
			this.UpdateDescription();
		}

		public override void Update()
		{
			if (this.m_activateCheckbox.IsClicked)
			{
				this.UpdateDescription();
			}
			if (this.m_okButton.IsClicked)
			{
				if (this.m_subsystemGreenNight != null)
				{
					bool newState = this.m_activateCheckbox.IsChecked;
					bool oldState = this.m_subsystemGreenNight.IsGreenNightEnabled;

					// Solo aplica cambios y muestra mensaje si el estado realmente cambió
					if (newState != oldState)
					{
						this.m_subsystemGreenNight.SetGreenNightActive(newState);
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
			this.m_descriptionLabel.Text = this.m_activateCheckbox.IsChecked ? DescriptionOn : DescriptionOff;
		}
	}
}

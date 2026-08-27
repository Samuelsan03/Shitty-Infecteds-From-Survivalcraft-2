using System;
using Engine;

namespace Game
{
	public interface INoiseAttraction
	{
		void AttractNoise(ComponentBody sourceBody, Vector3 sourcePosition, float loudness);
	}
}

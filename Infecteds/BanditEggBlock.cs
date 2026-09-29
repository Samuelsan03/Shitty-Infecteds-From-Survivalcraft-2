using System;
using System.Collections.Generic;
using System.Linq;
using Engine;
using Engine.Graphics;
using TemplatesDatabase;

namespace Game
{
	public class BanditEggBlock : Block
	{
		public static string fName = "BanditEggBlock";
		public static int Index = 532;

		public Dictionary<int, BanditEggBlock.EggType> m_eggTypes = new Dictionary<int, BanditEggBlock.EggType>();
		public Texture2D m_texture;

		public ReadOnlyList<BanditEggBlock.EggType> EggTypes
		{
			get
			{
				return new ReadOnlyList<BanditEggBlock.EggType>(this.m_eggTypes.Values.ToList<BanditEggBlock.EggType>());
			}
		}

		public override void Initialize()
		{
			this.m_eggTypes.Clear();

			// Cargar la textura personalizada del bandido
			m_texture = ContentManager.Get<Texture2D>("Textures/Gui/bandit");

			// Crear el egg type del bandido (index 0, único tipo)
			int num = 0;
			this.m_eggTypes[num] = new BanditEggBlock.EggType
			{
				EggTypeIndex = num,
				ShowEgg = true,
				DisplayName = "Bandit Egg",
				NutritionalValue = 0f,
				Color = Color.White,
				// ScaleUV (16, 16) => 0.0625 * 16 = 1.0 => UVs 0-1 (textura completa, no atlas)
				ScaleUV = new Vector2(16f, 16f),
				SwapUV = false,
				Scale = 1.5f,         // Tamaño del bloque (no confundir con icono)
				TextureSlot = 0
			};

			// Cargar el modelo del huevo y crear el BlockMesh
			Model model = ContentManager.Get<Model>("Models/Egg");
			Matrix boneAbsoluteTransform = BlockMesh.GetBoneAbsoluteTransform(model.FindMesh("Egg", true).ParentBone);

			foreach (BanditEggBlock.EggType eggType in this.m_eggTypes.Values)
			{
				if (eggType != null)
				{
					eggType.BlockMesh = new BlockMesh();
					eggType.BlockMesh.AppendModelMeshPart(
						model.FindMesh("Egg", true).MeshParts[0],
						boneAbsoluteTransform,
						false, false, false, false,
						eggType.Color
					);

					Matrix matrix = Matrix.Identity;
					if (eggType.SwapUV)
					{
						matrix.M11 = 0f;
						matrix.M12 = 1f;
						matrix.M21 = 1f;
						matrix.M22 = 0f;
					}
					matrix *= Matrix.CreateScale(0.0625f * eggType.ScaleUV.X, 0.0625f * eggType.ScaleUV.Y, 1f);
					matrix *= Matrix.CreateTranslation(
						(float)(eggType.TextureSlot % 16) / 16f,
						(float)(eggType.TextureSlot / 16) / 16f,
						0f
					);
					eggType.BlockMesh.TransformTextureCoordinates(matrix, -1);
				}
			}

			base.Initialize();
		}

		public override Texture2D GetDefaultTexture(int value)
		{
			return m_texture;
		}

		public override string GetDisplayName(SubsystemTerrain subsystemTerrain, int value)
		{
			BanditEggBlock.EggType eggType = this.GetEggType(Terrain.ExtractData(value));
			int data = Terrain.ExtractData(value);
			bool isCooked = BanditEggBlock.GetIsCooked(data);
			bool isLaid = BanditEggBlock.GetIsLaid(data);
			if (isCooked)
			{
				return string.Format("Cooked {0}", eggType.DisplayName);
			}
			if (!isLaid)
			{
				return eggType.DisplayName;
			}
			return string.Format("Laid {0}", eggType.DisplayName);
		}

		public override string GetCategory(int value)
		{
			return "Spawner Eggs";
		}

		public override float GetNutritionalValue(int value)
		{
			BanditEggBlock.EggType eggType = this.GetEggType(Terrain.ExtractData(value));
			if (!BanditEggBlock.GetIsCooked(Terrain.ExtractData(value)))
			{
				return eggType.NutritionalValue;
			}
			return 1.5f * eggType.NutritionalValue;
		}

		public override float GetSicknessProbability(int value)
		{
			if (!BanditEggBlock.GetIsCooked(Terrain.ExtractData(value)))
			{
				return this.DefaultSicknessProbability;
			}
			return 0f;
		}

		public override int GetRotPeriod(int value)
		{
			if (this.GetNutritionalValue(value) > 0f)
			{
				return base.GetRotPeriod(value);
			}
			return 0;
		}

		public override int GetDamage(int value)
		{
			return Terrain.ExtractData(value) >> 16 & 1;
		}

		public override int SetDamage(int value, int damage)
		{
			int num = Terrain.ExtractData(value);
			num = ((num & -65537) | (damage & 1) << 16);
			return Terrain.ReplaceData(value, num);
		}

		public override int GetDamageDestructionValue(int value)
		{
			return 246;
		}

		public override IEnumerable<int> GetCreativeValues()
		{
			yield return Terrain.MakeBlockValue(BanditEggBlock.Index, 0, 0);
		}

		public override IEnumerable<CraftingRecipe> GetProceduralCraftingRecipes()
		{
			yield break;
		}

		public override void GenerateTerrainVertices(BlockGeometryGenerator generator, TerrainGeometry geometry, int value, int x, int y, int z)
		{
		}

		public override void DrawBlock(PrimitivesRenderer3D primitivesRenderer, int value, Color color, float size, ref Matrix matrix, DrawBlockEnvironmentData environmentData)
		{
			int data = Terrain.ExtractData(value);
			BanditEggBlock.EggType eggType = this.GetEggType(data);
			Texture2D defaultTexture = this.GetDefaultTexture(value);
			if (defaultTexture == null)
			{
				BlocksManager.DrawMeshBlock(primitivesRenderer, eggType.BlockMesh, color, eggType.Scale * size, ref matrix, environmentData);
				return;
			}
			BlocksManager.DrawMeshBlock(primitivesRenderer, eggType.BlockMesh, defaultTexture, color, eggType.Scale * size, ref matrix, environmentData);
		}

		public BanditEggBlock.EggType GetEggType(int data)
		{
			int key = data >> 4 & 4095;
			BanditEggBlock.EggType result;
			if (this.m_eggTypes.TryGetValue(key, out result))
			{
				return result;
			}
			return this.m_eggTypes[0];
		}

		public BanditEggBlock.EggType GetEggTypeByCreatureTemplateName(string templateName)
		{
			return this.m_eggTypes.FirstOrDefault(
				(KeyValuePair<int, BanditEggBlock.EggType> pair) => pair.Value.TemplateName == templateName
			).Value;
		}

		public static bool GetIsCooked(int data)
		{
			return (data & 1) != 0;
		}

		public static int SetIsCooked(int data, bool isCooked)
		{
			if (!isCooked)
			{
				return data & -2;
			}
			return data | 1;
		}

		public static bool GetIsLaid(int data)
		{
			return (data & 2) != 0;
		}

		public static int SetIsLaid(int data, bool isLaid)
		{
			if (!isLaid)
			{
				return data & -3;
			}
			return data | 2;
		}

		public static int SetEggType(int data, int eggTypeIndex)
		{
			data &= -65521;
			data |= (eggTypeIndex & 4095) << 4;
			return data;
		}

		public class EggType
		{
			public int EggTypeIndex;
			public bool ShowEgg;
			public string DisplayName;
			public string TemplateName;
			public float NutritionalValue;
			public int TextureSlot;
			public Color Color;
			public Vector2 ScaleUV;
			public bool SwapUV;
			public float Scale;
			public BlockMesh BlockMesh;
		}
	}
}

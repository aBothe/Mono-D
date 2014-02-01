using MonoDevelop.Components.Commands;
using MonoDevelop.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MonoDevelop.D.Highlighting
{
	public class ToggleDiffbasedHighlighthandler : CommandHandler
	{
		public const string ToogleCmdId = "ToggleCmdId";

		protected override void Run()
		{
			DiffbasedHighlighting.Enabled = !DiffbasedHighlighting.Enabled;
		}
	}

	class DiffbasedHighlighting
	{
		public const string DiffBasedHighlightingProp = "DiffbasedHighlighting";
		public static bool Enabled
		{
			get
			{
				return PropertyService.Get(DiffBasedHighlightingProp, false);
			}
			set
			{
				PropertyService.Set(DiffBasedHighlightingProp, value);
			}
		}

		struct HSV
			{
				public double h;
				public double s;
				public double v;

				public HSV(double h, double s, double v)
				{
					this.h = h;
					this.s = s;
					this.v = v;
				}

				public static implicit operator Cairo.Color(HSV hsv)
				{
					double r = 0, g = 0, b = 0;

					double p, q, t, ff;

					if (hsv.s <= 0.0)
					{       // < is bogus, just shuts up warnings
						r = hsv.v;
						g = hsv.v;
						b = hsv.v;
						return new Cairo.Color(r, g, b);
					}

					var hh = hsv.h;
					if (hh >= 360.0)
						hh = 0.0;
					hh /= 60.0;
					var i = (int)hh;
					ff = hh - i;
					p = hsv.v * (1.0 - hsv.s);
					q = hsv.v * (1.0 - (hsv.s * ff));
					t = hsv.v * (1.0 - (hsv.s * (1.0 - ff)));

					switch (i)
					{
						case 0:
							r = hsv.v;
							g = t;
							b = p;
							break;
						case 1:
							r = q;
							g = hsv.v;
							b = p;
							break;
						case 2:
							r = p;
							g = hsv.v;
							b = t;
							break;

						case 3:
							r = p;
							g = q;
							b = hsv.v;
							break;
						case 4:
							r = t;
							g = p;
							b = hsv.v;
							break;
						case 5:
						default:
							r = hsv.v;
							g = p;
							b = q;
							break;
					}
					return new Cairo.Color(r, g, b);
				}
			}

			static List<int> colorUsed = new List<int>{
					26, // m_ prefix 
					17, // _ prefix
					35, // i,j,k
				};
			static Dictionary<string, HSV> colorPrefixGroups = new Dictionary<string, HSV>{
				{"m_", new HSV(150.0, 0.99, 0.6)},
				{"_", new HSV(225.0, 0.99, 0.6)},
			};
			static Dictionary<string, double> nextPrefixGroupValue = new Dictionary<string, double> { 
				{"m_",0.6},
				{"_",0.6},
			};
			static Dictionary<string, double> nextPrefixGroupSaturation = new Dictionary<string, double>{ 
				{"m_",0.95},
				{"_",0.95},
			};
			static Dictionary<int, HSV> colorCache = new Dictionary<int, HSV> { 
				{"i".GetHashCode(), new HSV(300.0, 0.99, 0.6)},
				{"j".GetHashCode(), new HSV(300.0, 0.99, 0.55)},
				{"k".GetHashCode(), new HSV(300.0, 0.99, 0.5)},
			};
			static List<HSV> palette = new List<HSV>();
			static double[] excludeHues = { 50.0, 75.0, 100.0 };

			static DiffbasedHighlighting()
			{
				for (int i = 0; i <= 15; i++)
				{
					if (!excludeHues.Contains(i * 25.0))
					{ // remove some too light colors
						palette.Add(new HSV(i * 25.0, 0.6, 0.99));
					}
					palette.Add(new HSV(i * 25.0, 0.8, 0.8));
					palette.Add(new HSV(i * 25.0, 0.99, 0.6));
				}
				/* Uncomment this to see the grouping colors
				foreach (i; iota(0.0,0.4,0.05)){
		
					HSV col3 = { h: 225.0, s: 0.99, v: 0.6+i };
					palette ~= col3;
				}
				*/
			}

			public static Cairo.Color GetColor(string str)
			{
				var hash = str.GetHashCode();
				HSV col;
				if (colorCache.TryGetValue(hash, out col))
					return col;

				foreach (var kv in colorPrefixGroups)
				{
					var key = kv.Key;
					if (str.StartsWith(key))
					{
						col = kv.Value;
						col.v = nextPrefixGroupValue[key];
						col.s = nextPrefixGroupSaturation[key];
						if (nextPrefixGroupValue[key] < 1.00 && nextPrefixGroupValue[key] >= 0.55)
							nextPrefixGroupValue[key] += 0.05; // lighten it up a bit for the next var in this group
						else if (nextPrefixGroupValue[key] >= 1.00)
							nextPrefixGroupValue[key] = 0.50;
						else if (nextPrefixGroupValue[key] <= 0.20)
						{
							nextPrefixGroupSaturation[key] -= 0.05;
						}
						else if (nextPrefixGroupSaturation[key] <= 0.20)
						{
							nextPrefixGroupValue[key] = 0.60;
							nextPrefixGroupSaturation[key] = 0.60;
						}
						return colorCache[hash] = col;
					}
				}

				var match = 255 - (hash & 0xFF); // 0..255
				var @base = ((double)match / 255.0) * 360.0; // hue is chosen from hash

				int lastUsed = 0;
				for (int i = 0; i < palette.Count; i++)
				{
					col = palette[i];
					if (colorUsed.Contains(i))
						lastUsed = 0;
					else
						++lastUsed;

					if ((@base - col.h) <= 360.0 / (palette.Count / 2.0))
					{ // select the nearest hue in palette
						if (lastUsed <= 3 && lastUsed >= 0) // either used or too near a used one
						{
							bool colorFound = false;

							for (int k = 0; k < palette.Count; k++)
							{
								if (k <= i + 3) // git some room to change hue more obviously
									continue;
								if (!colorUsed.Contains(k)) // color isn't used
								{
									colorFound = true;
									col = palette[k];
									break;
								}
							}
							if(!colorFound)
							for (int k = palette.Count - 2; k > 0; k--)
							{	// start from the beginning
								if (!colorUsed.Contains(k)) // color isn't used
								{
									colorFound = true;
									col = palette[k];
									break;
								}
							}

							if(!colorFound)
								return new Cairo.Color(((hash >> 16) & 0xFF) / 255.0, ((hash >> 8) & 0xFF) / 255.0, (hash & 0xFF) / 255.0);
						}
						
						if (!colorUsed.Contains(i))
							colorUsed.Add(i);
						colorCache[hash] = col;

						return col;
					}
				}

				return palette[palette.Count - 1];
			}
	}
}

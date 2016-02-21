using System;
using Newtonsoft.Json;
using System.IO;

namespace MonoDevelop.D.Projects.Dub.DefinitionFormats
{
	public class JSONThingDeserializer
	{
		public JSONObject Deserialize(TextReader tr)
		{
			using (var r = new JsonTextReader (tr)) {
				r.Read ();
				return ReadObject (r);
			}
		}

		JSONObject ReadObject(JsonReader r)
		{
			if (r.TokenType != JsonToken.StartObject)
				throw new InvalidDataException ("Object begin expected");
			r.Read ();

			var o = new JSONObject();
			while (r.TokenType == JsonToken.PropertyName) {
				var name = (r.Value as string).ToLowerInvariant();
				r.Read ();
				var thing = ReadThing (r);

				o.Properties.Add (name, thing);
			}

			if (r.TokenType != JsonToken.EndObject)
				throw new InvalidDataException ("Object end expected");
			r.Read ();

			return o;
		}

		JSONThing ReadThing(JsonReader r)
		{
			switch (r.TokenType) {
				case JsonToken.Null:
					r.Read ();
					return new JSONValueLeaf{ Value = string.Empty };
				case JsonToken.Comment:
					r.Read ();
					return ReadThing (r);
				case JsonToken.Integer:
				case JsonToken.Float:
				case JsonToken.Boolean:
				case JsonToken.String:
					var ret = new JSONValueLeaf{ Value = r.Value.ToString() };
					r.Read ();
					return ret;
				case JsonToken.StartObject:
					return ReadObject(r);
				case JsonToken.StartArray:
					return ReadArray(r);
				default:
					throw new InvalidDataException("Object, array or string value expected");
			}
		}

		JSONArray ReadArray(JsonReader r)
		{
			if (r.TokenType != JsonToken.StartArray)
				throw new InvalidDataException ("Array begin expected");
			r.Read ();

			var array = new JSONArray ();
			while (r.TokenType != JsonToken.EndArray) {
				array.Items.Add (ReadThing (r));
			}

			if (r.TokenType != JsonToken.EndArray)
				throw new InvalidDataException ("Array end expected");
			r.Read ();

			return array;
		}
	}
}


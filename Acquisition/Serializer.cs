﻿using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using LLibrary;

namespace Acquisition
{
    public static class Serializer
    {
        public const string GasNamesSerializationName = "gases";
        public const string JsonExtension = ".json";

        public static void Serialize<T>(T obj, string name, JsonConverter converter = null)
        {
            try
            {
                var p = Path.Combine(Environment.CurrentDirectory, name + JsonExtension);
                File.WriteAllText(p, JsonConvert.SerializeObject(obj, converter));
            }
            catch (Exception ex)
            {
                Program.Log("Serialization error:", ex);
            }
        }

        public static T Deserialize<T>(string name, T def, JsonConverter converter = null)
        {
            try
            {
                var p = Path.Combine(Environment.CurrentDirectory, name + JsonExtension);
                if (File.Exists(p))
                {
                    object o;
                    if (converter == null)
                    {
                        o = JsonConvert.DeserializeObject(File.ReadAllText(p));
                    }
                    else
                    {
                        o = JsonConvert.DeserializeObject(File.ReadAllText(p), typeof(T), converter);
                    }
                    if (o == null) throw new JsonSerializationException();
                    return (T)o;
                }
                else
                {
                    Serialize(def, name, converter);
                }
            }
            catch (Exception ex)
            {
                Program.Log("Deserialization error:", ex);
            }
            return def;
        }
    }
}

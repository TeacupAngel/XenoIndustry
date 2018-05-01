using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

using UnityEngine;

using SimpleJSON;

namespace XenoIndustry
{
    public static class JSONUtil
    {
        public static JSONNode[] ReadJSONFile(string filename)
        {
            if (!File.Exists(filename))
            {
                Debug.Log(String.Format("JSONUtil: file {0} does not exist!", filename));

                return null;
            }

            List<string> JSONItems = new List<string>();
            StringBuilder stringBuilder = new StringBuilder();

            int currentBrackets = 0;
            bool isReadingItem = false;

            using (StreamReader reader = new StreamReader(filename))
            {
                if (reader == null)
                {
                    Debug.Log(String.Format("JSONUtil: failed to read file {0}!", filename));

                    return null;
                }

                string JSONFile = reader.ReadToEnd();

                foreach (char currentChar in JSONFile)
                {
                    if (currentChar == '{')
                    {
                        currentBrackets++;
                        isReadingItem = true;
                    }
                    else if (currentChar == '}')
                    {
                        currentBrackets--;
                    }

                    if (isReadingItem && currentChar != ' ' && currentChar != '\n' && currentChar != '\r' && currentChar != '\t')
                    {
                        stringBuilder.Append(currentChar);

                        if (currentBrackets == 0)
                        {
                            JSONItems.Add(stringBuilder.ToString());
                            stringBuilder = new StringBuilder();

                            isReadingItem = false;
                        }
                    }
                }
            }

            JSONNode[] result = new JSONNode[JSONItems.Count];

            for (int i = 0; i < JSONItems.Count; i++)
            {
                result[i] = JSON.Parse(JSONItems[i]);
            }

            return result;
        }
    }
}

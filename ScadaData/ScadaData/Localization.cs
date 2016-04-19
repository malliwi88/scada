﻿/*
 * Copyright 2016 Mikhail Shiryaev
 * 
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 * 
 *     http://www.apache.org/licenses/LICENSE-2.0
 * 
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 * 
 * 
 * Product  : Rapid SCADA
 * Module   : ScadaData
 * Summary  : Localization mechanism
 * 
 * Author   : Mikhail Shiryaev
 * Created  : 2014
 * Modified : 2016
 */

using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Web.UI;
using System.Web.UI.WebControls;
using System.Xml;
using Microsoft.Win32;
using WinForms = System.Windows.Forms;

namespace Scada
{
    /// <summary>
    /// Localization mechanism
    /// <para>Механизм локализации</para>
    /// </summary>
    public static class Localization
    {
        /// <summary>
        /// Словарь
        /// </summary>
        public class Dict
        {
            /// <summary>
            /// Конструктор
            /// </summary>
            private Dict()
            {

            }
            /// <summary>
            /// Конструктор
            /// </summary>
            public Dict(string key)
            {
                Key = key;
                Phrases = new Dictionary<string, string>();
            }

            /// <summary>
            /// Получить ключ словаря
            /// </summary>
            public string Key { get; private set; }
            /// <summary>
            /// Получить фразы, содержащиеся в словаре, по их ключам
            /// </summary>
            public Dictionary<string, string> Phrases { get; private set; }

            /// <summary>
            /// Получить имя файла словаря для заданной культуры
            /// </summary>
            public static string GetFileName(string directory, string fileNamePrefix, CultureInfo cultureInfo)
            {
                return directory + fileNamePrefix +
                    (cultureInfo == null || string.IsNullOrEmpty(cultureInfo.Name) ? "" : "." + cultureInfo.Name) + 
                    ".xml";
            }
            /// <summary>
            /// Получить фразу из словаря по ключу или значение по умолчанию при её отсутствии
            /// </summary>
            public string GetPhrase(string key, string defaultVal)
            {
                return Phrases.ContainsKey(key) ? Phrases[key] : defaultVal;
            }
            /// <summary>
            /// Получить пустую фразу для заданного ключа
            /// </summary>
            public static string GetEmptyPhrase(string key)
            {
                return string.Format(UseRussian ?
                    "Фраза с ключом {0} не загружена." :
                    "The phrase with the key {0} is not loaded.", key);
            }
        }


        /// <summary>
        /// Конструктор
        /// </summary>
        static Localization()
        {
            ReadCulture();
            Dictionaries = new Dictionary<string, Dict>();
        }


        /// <summary>
        /// Получить информацию о культуре всех приложений SCADA
        /// </summary>
        public static CultureInfo Culture { get; private set; }

        /// <summary>
        /// Получить признак использования русской локализации
        /// </summary>
        public static bool UseRussian { get; private set; }

        /// <summary>
        /// Получить загруженные словари для локализации
        /// </summary>
        public static Dictionary<string, Dict> Dictionaries { get; private set; }

        /// <summary>
        /// Получить признак, что запись дня должна располагаться после записи месяца
        /// </summary>
        public static bool DayAfterMonth
        {
            get
            {
                string pattern = Localization.Culture.DateTimeFormat.ShortDatePattern.ToLowerInvariant();
                return pattern.IndexOf('m') < pattern.IndexOf('d');
            }
        }


        /// <summary>
        /// Считать информацию о культуре из реестра
        /// </summary>
        private static void ReadCulture()
        {
            try
            {
                using (RegistryKey key = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64).
                    OpenSubKey("Software\\SCADA", false))
                    Culture = CultureInfo.GetCultureInfo(key.GetValue("Culture").ToString());
            }
            catch
            {
                Culture = CultureIsRussian(CultureInfo.CurrentCulture) ? 
                    CultureInfo.GetCultureInfo("ru-RU") : CultureInfo.GetCultureInfo("en-GB");
            }
            finally
            {
                UseRussian = CultureIsRussian(Culture);
            }
        }

        /// <summary>
        /// Проверить, что имя культуры соответствует русской культуре
        /// </summary>
        private static bool CultureIsRussian(CultureInfo cultureInfo)
        {
            return cultureInfo.Name == "ru" || cultureInfo.Name.StartsWith("ru-", StringComparison.OrdinalIgnoreCase);
        }


        /// <summary>
        /// Записать информацию о культуре в реестр
        /// </summary>
        public static bool WriteCulture(string cultureName, out string errMsg)
        {
            try
            {
                using (RegistryKey key = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64).
                    CreateSubKey("Software\\SCADA"))
                    key.SetValue("Culture", cultureName);
                errMsg = "";
                return true;
            }
            catch (Exception ex)
            {
                errMsg = (UseRussian ? "Ошибка при записи информации о культуре в реестр: " : 
                    "Error writing culture info to the registry: ") + ex.Message;
                return false;
            }
        }

        /// <summary>
        /// Получить имя файла словаря в зависимости от используемой культуры
        /// </summary>
        public static string GetDictionaryFileName(string directory, string fileNamePrefix)
        {
            return Dict.GetFileName(directory, fileNamePrefix, Culture);
        }

        /// <summary>
        /// Загрузить словари для считанной культуры
        /// </summary>
        /// <remarks>Если ключ загружаемого словаря совпадает с ключом уже загруженного, то словари сливаются.
        /// Если совпадают ключи фраз, то новое значение фразы записывается поверх старого</remarks>
        public static bool LoadDictionaries(string directory, string fileNamePrefix, out string errMsg)
        {
            string fileName = GetDictionaryFileName(directory, fileNamePrefix);
            return LoadDictionaries(fileName, out errMsg);
        }

        /// <summary>
        /// Загрузить словари для считанной культуры
        /// </summary>
        public static bool LoadDictionaries(string fileName, out string errMsg)
        {
            if (File.Exists(fileName))
            {
                try
                {
                    XmlDocument xmlDoc = new XmlDocument();
                    xmlDoc.Load(fileName);

                    XmlNodeList dictNodeList = xmlDoc.DocumentElement.SelectNodes("Dictionary");
                    foreach (XmlElement dictElem in dictNodeList)
                    {
                        Dict dict;
                        string dictKey = dictElem.GetAttribute("key");

                        if (!Dictionaries.TryGetValue(dictKey, out dict))
                        {
                            dict = new Dict(dictKey);
                            Dictionaries.Add(dictKey, dict);
                        }

                        XmlNodeList phraseNodeList = dictElem.SelectNodes("Phrase");
                        foreach (XmlElement phraseElem in phraseNodeList)
                        {
                            string phraseKey = phraseElem.GetAttribute("key");
                            dict.Phrases[phraseKey] = phraseElem.InnerText;
                        }
                    }

                    errMsg = "";
                    return true;
                }
                catch (Exception ex)
                {
                    errMsg = (UseRussian ? 
                        "Ошибка при загрузке словарей: " : 
                        "Error loading dictionaries: ") + ex.Message;
                    return false;
                }
            }
            else
            {
                errMsg = (UseRussian ? 
                    "Не найден файл словарей: " : 
                    "Dictionary file not found: ") + fileName;
                return false;
            }
        }

        /// <summary>
        /// Определить, что загрузка словаря необходима: 
        /// не используется русская локализация или существует файл словаря
        /// </summary>
        [Obsolete("Load dictionary anyway.")]
        public static bool LoadingRequired(string directory, string fileNamePrefix)
        {
            return !UseRussian || File.Exists(GetDictionaryFileName(directory, fileNamePrefix));
        }

        /// <summary>
        /// Преобразовать дату и время в строку с использованием информации о культуре
        /// </summary>
        public static string ToLocalizedString(this DateTime dateTime)
        {
            return dateTime.ToString("d", Culture) + " " + dateTime.ToString("T", Culture);
        }
    }
}
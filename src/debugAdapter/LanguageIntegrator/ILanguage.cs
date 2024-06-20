﻿// Copyright (c) Ivan Bondarev, Stanislav Mikhalkovich (for details please see \doc\copyright.txt)
// This code is distributed under the GNU LGPL (for details please see \doc\license.txt)

using System.Collections.Generic;
using PascalABCCompiler.Parsers;

namespace Languages.Facade
{
    /// <summary>
    /// Интерфейс языка программирования, поддерживаемого платформой
    /// </summary>
    public interface ILanguage
    {
        /// <summary>
        /// Название языка
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Версия языка
        /// </summary>
        string Version { get; }

        /// <summary>
        /// Авторское право
        /// </summary>
        string Copyright { get; }

        /// <summary>
        /// Основной парсер языка
        /// </summary>
        IParser Parser { get; }

        /// <summary>
        /// Парсер документирующих комментариев языка
        /// </summary>
        IDocParser DocParser { get; }

        /// <summary>
        /// Постпреобразования синтаксического дерева (синтаксический сахар и др.)
        /// </summary>

        /// <summary>
        /// Преобразователь из синтаксического дерева в семантическое
        /// </summary>
       

        /// <summary>
        /// Расширения файлов, относящиеся к языку
        /// </summary>
        string[] FilesExtensions { get; }

        /// <summary>
        /// Чувствительность к регистру
        /// </summary>
        bool CaseSensitive { get; }

        /// <summary>
        /// Названия системных модулей (стандартной библиотеки и др.)
        /// </summary>
        string[] SystemUnitNames { get; }

        /// <summary>
        /// Задание семантических констант, частично задающих семантические правила языка (поля класса SemanticRules)
        /// </summary>
        void SetSemanticConstants();

        /// <summary>
        /// Обновление SyntaxTreeToSemanticTreeConverter (нужно при обновлении компилятора)
        /// </summary>
        // TODO: Возможно, стоит переделать логику обновления, чтобы не приходилось создавать новый объект каждый раз  EVA
        void SetSyntaxTreeToSemanticTreeConverter();

    }
}

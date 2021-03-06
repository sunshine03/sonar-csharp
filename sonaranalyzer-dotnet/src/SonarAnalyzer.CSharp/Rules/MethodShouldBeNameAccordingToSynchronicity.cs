﻿/*
 * SonarAnalyzer for .NET
 * Copyright (C) 2015-2018 SonarSource SA
 * mailto: contact AT sonarsource DOT com
 *
 * This program is free software; you can redistribute it and/or
 * modify it under the terms of the GNU Lesser General Public
 * License as published by the Free Software Foundation; either
 * version 3 of the License, or (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the GNU
 * Lesser General Public License for more details.
 *
 * You should have received a copy of the GNU Lesser General Public License
 * along with this program; if not, write to the Free Software Foundation,
 * Inc., 51 Franklin Street, Fifth Floor, Boston, MA  02110-1301, USA.
 */

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using SonarAnalyzer.Common;
using SonarAnalyzer.Helpers;

namespace SonarAnalyzer.Rules.CSharp
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    [Rule(DiagnosticId)]
    public sealed class MethodShouldBeNameAccordingToSynchronicity : SonarDiagnosticAnalyzer
    {
        internal const string DiagnosticId = "S4261";
        private const string MessageFormat = "{0}";
        private const string AddAsyncSuffixMessage = "Add the 'Async' suffix to the name of this method.";
        private const string RemoveAsyncSuffixMessage = "Remove the 'Async' suffix to the name of this method.";

        private static readonly DiagnosticDescriptor rule =
            DiagnosticDescriptorBuilder.GetDescriptor(DiagnosticId, MessageFormat, RspecStrings.ResourceManager);
        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(rule);

        private static ISet<KnownType> TaskTypes = new HashSet<KnownType>
        {
            KnownType.System_Threading_Tasks_Task,
            KnownType.System_Threading_Tasks_Task_T,
            KnownType.System_Threading_Tasks_ValueTask_TResult
        };

        private static readonly ISet<KnownType> TestMethodAttributes = new HashSet<KnownType>
        {
            KnownType.Microsoft_VisualStudio_TestTools_UnitTesting_TestMethodAttribute,
            KnownType.Microsoft_VisualStudio_TestTools_UnitTesting_DataTestMethodAttribute,
            KnownType.NUnit_Framework_TestAttribute,
            KnownType.NUnit_Framework_TestCaseAttribute,
            KnownType.NUnit_Framework_TestCaseSourceAttribute,
            KnownType.NUnit_Framework_TheoryAttribute,
            KnownType.Xunit_FactAttribute,
            KnownType.Xunit_TheoryAttribute,
            KnownType.LegacyXunit_TheoryAttribute,
        };

        protected override void Initialize(SonarAnalysisContext context)
        {
            context.RegisterSyntaxNodeActionInNonGenerated(
                c =>
                {
                    var methodDeclaration = (MethodDeclarationSyntax)c.Node;
                    if (methodDeclaration.Identifier.IsMissing)
                    {
                        return;
                    }

                    var methodSymbol = c.SemanticModel.GetDeclaredSymbol(methodDeclaration);
                    if (methodSymbol == null ||
                        methodSymbol.GetInterfaceMember() != null ||
                        methodSymbol.GetOverriddenMember() != null ||
                        methodSymbol.HasAnyAttribute(TestMethodAttributes))
                    {
                        return;
                    }

                    var isTaskReturnType = (methodSymbol.ReturnType as INamedTypeSymbol)?.ConstructedFrom.DerivesFromAny(TaskTypes) ?? false;
                    var hasAsyncSuffix = methodDeclaration.Identifier.ValueText.SplitCamelCaseToWords().LastOrDefault() == "ASYNC";

                    if (hasAsyncSuffix && !isTaskReturnType)
                    {
                        c.ReportDiagnosticWhenActive(Diagnostic.Create(rule, methodDeclaration.Identifier.GetLocation(), RemoveAsyncSuffixMessage));
                    }
                    else if (!hasAsyncSuffix && isTaskReturnType)
                    {
                        c.ReportDiagnosticWhenActive(Diagnostic.Create(rule, methodDeclaration.Identifier.GetLocation(), AddAsyncSuffixMessage));
                    }
                    else
                    {
                        // do nothing
                    }
                },
                SyntaxKind.MethodDeclaration);
        }
    }
}

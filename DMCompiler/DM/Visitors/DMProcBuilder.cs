using OpenDreamShared.Compiler;
using DMCompiler.Compiler.DM;
using System.Collections.Generic;
using OpenDreamShared.Dream;
using System;
using DMCompiler.DM.Expressions;
using OpenDreamShared.Dream.Procs;

namespace DMCompiler.DM.Visitors {
    class DMProcBuilder {
        private readonly DMObject _dmObject;
        private readonly DMProc _proc;

        public DMProcBuilder(DMObject dmObject, DMProc proc) {
            _dmObject = dmObject;
            _proc = proc;
        }

        public void ProcessProcDefinition(DMASTProcDefinition procDefinition) {
            if (procDefinition.Body == null) return;

            _proc.DebugSource(procDefinition.Location.SourceFile);

            foreach (DMASTDefinitionParameter parameter in procDefinition.Parameters) {
                string parameterName = parameter.Name;

                if (parameter.Value != null) { //Parameter has a default value
                    string afterDefaultValueCheck = _proc.NewLabelName();
                    DMReference parameterRef = _proc.GetLocalVariableReference(parameterName);

                    //Don't set parameter to default if not null
                    _proc.PushReferenceValue(parameterRef);
                    _proc.IsNull();
                    _proc.JumpIfFalse(afterDefaultValueCheck);

                    //Set default
                    try {
                        DMExpression.Emit(_dmObject, _proc, parameter.Value, parameter.ObjectType);
                    } catch (CompileErrorException e) {
                        DMCompiler.Emit(e.Error);
                    }
                    _proc.Assign(parameterRef);
                    _proc.Pop();

                    _proc.AddLabel(afterDefaultValueCheck);
                }
            }

            ProcessBlockInner(procDefinition.Body);
            _proc.ResolveLabels();
        }

        public void ProcessBlockInner(DMASTProcBlockInner block) {
            // TODO ProcessStatementSet() needs to be before any loops but this is nasty
            foreach (var stmt in block.Statements) {
                if (stmt is DMASTProcStatementSet set) {
                    try {
                        ProcessStatementSet(set);
                    } catch (CompileAbortException e) {
                        // The statement's location info isn't passed all the way down so change the error to make it more accurate
                        e.Error.Location = set.Location;
                        DMCompiler.Emit(e.Error);
                        return; // Don't spam the error that will continue to exist
                    } catch (CompileErrorException e) {
                        //Retreat from the statement when there's an error
                        DMCompiler.Emit(e.Error);
                    }
                }
            }

            foreach (DMASTProcStatement statement in block.Statements) {
                // see above
                if (statement is DMASTProcStatementSet) {
                    continue;
                }

                if (statement.Location.Line != null) {
                    _proc.DebugLine(statement.Location.Line.Value);
                }

                try {
                    ProcessStatement(statement);
                } catch (CompileAbortException e) {
                    // The statement's location info isn't passed all the way down so change the error to make it more accurate
                    e.Error.Location = statement.Location;
                    DMCompiler.Emit(e.Error);
                    return; // Don't spam the error that will continue to exist
                } catch (CompileErrorException e) {
                    //Retreat from the statement when there's an error
                    DMCompiler.Emit(e.Error);
                }
            }
        }

        public void ProcessStatement(DMASTProcStatement statement) {
            switch (statement) {
                case DMASTProcStatementExpression statementExpression: ProcessStatementExpression(statementExpression); break;
                case DMASTProcStatementContinue statementContinue: ProcessStatementContinue(statementContinue); break;
                case DMASTProcStatementGoto statementGoto: ProcessStatementGoto(statementGoto); break;
                case DMASTProcStatementLabel statementLabel: ProcessStatementLabel(statementLabel); break;
                case DMASTProcStatementBreak statementBreak: ProcessStatementBreak(statementBreak); break;
                case DMASTProcStatementDel statementDel: ProcessStatementDel(statementDel); break;
                case DMASTProcStatementSpawn statementSpawn: ProcessStatementSpawn(statementSpawn); break;
                case DMASTProcStatementReturn statementReturn: ProcessStatementReturn(statementReturn); break;
                case DMASTProcStatementIf statementIf: ProcessStatementIf(statementIf); break;
                case DMASTProcStatementFor statementFor: ProcessStatementFor(statementFor); break;
                case DMASTProcStatementInfLoop statementInfLoop: ProcessStatementInfLoop(statementInfLoop); break;
                case DMASTProcStatementWhile statementWhile: ProcessStatementWhile(statementWhile); break;
                case DMASTProcStatementDoWhile statementDoWhile: ProcessStatementDoWhile(statementDoWhile); break;
                case DMASTProcStatementSwitch statementSwitch: ProcessStatementSwitch(statementSwitch); break;
                case DMASTProcStatementBrowse statementBrowse: ProcessStatementBrowse(statementBrowse); break;
                case DMASTProcStatementBrowseResource statementBrowseResource: ProcessStatementBrowseResource(statementBrowseResource); break;
                case DMASTProcStatementOutputControl statementOutputControl: ProcessStatementOutputControl(statementOutputControl); break;
                case DMASTProcStatementOutput statementOutput: ProcessStatementOutput(statementOutput); break;
                case DMASTProcStatementInput statementInput: ProcessStatementInput(statementInput); break;
                case DMASTProcStatementVarDeclaration varDeclaration: ProcessStatementVarDeclaration(varDeclaration); break;
                case DMASTProcStatementTryCatch tryCatch: ProcessStatementTryCatch(tryCatch); break;
                case DMASTProcStatementThrow dmThrow: ProcessStatementThrow(dmThrow); break;
                case DMASTProcStatementMultipleVarDeclarations multipleVarDeclarations: {
                    foreach (DMASTProcStatementVarDeclaration varDeclaration in multipleVarDeclarations.VarDeclarations) {
                        ProcessStatementVarDeclaration(varDeclaration);
                    }

                    break;
                }
                default: throw new CompileAbortException(statement.Location, "Invalid proc statement");
            }
        }

        public void ProcessStatementExpression(DMASTProcStatementExpression statement) {
            DMExpression.Emit(_dmObject, _proc, statement.Expression);
            _proc.Pop();
        }

        public void ProcessStatementContinue(DMASTProcStatementContinue statementContinue) {
            _proc.Continue(statementContinue.Label);
        }

        public void ProcessStatementGoto(DMASTProcStatementGoto statementGoto) {
            _proc.Goto(statementGoto.Label.Identifier);
        }

        public void ProcessStatementLabel(DMASTProcStatementLabel statementLabel) {
            _proc.AddLabel(statementLabel.Name + "_codelabel");
            if (statementLabel.Body is not null)
            {
                _proc.StartScope();
                {
                    ProcessBlockInner(statementLabel.Body);
                }
                _proc.EndScope();
                _proc.AddLabel(statementLabel.Name + "_end");
            }
        }

        public void ProcessStatementBreak(DMASTProcStatementBreak statementBreak) {
            _proc.Break(statementBreak.Label);
        }

        public void ProcessStatementSet(DMASTProcStatementSet statementSet)
        {
            var attribute = statementSet.Attribute.ToLower();
            // TODO deal with "src"
            if (!DMExpression.TryConstant(_dmObject, _proc, statementSet.Value, out var constant) && attribute != "src") {
                throw new CompileErrorException(statementSet.Location, $"{attribute} attribute should be a constant");
            }

            switch (statementSet.Attribute.ToLower()) {
                case "waitfor": {
                    _proc.WaitFor(constant.IsTruthy());
                    break;
                }
                case "opendream_unimplemented": {
                    if (constant.IsTruthy())
                    {
                        _proc.Attributes |= ProcAttributes.Unimplemented;
                    }
                    else
                    {
                        _proc.Attributes &= ~ProcAttributes.Unimplemented;
                    }
                    break;
                }
                case "hidden":
                    if (constant.IsTruthy())
                    {
                        _proc.Attributes |= ProcAttributes.Hidden;
                    }
                    else
                    {
                        _proc.Attributes &= ~ProcAttributes.Hidden;
                    }
                    break;
                case "popup_menu":
                    if (constant.IsTruthy()) // The default is to show it so we flag it if it's hidden
                    {
                        _proc.Attributes &= ~ProcAttributes.HidePopupMenu;
                    }
                    else
                    {
                        _proc.Attributes |= ProcAttributes.HidePopupMenu;
                    }

                    DMCompiler.UnimplementedWarning(statementSet.Location, "set popup_menu is not implemented");
                    break;
                case "instant":
                    if (constant.IsTruthy())
                    {
                        _proc.Attributes |= ProcAttributes.Instant;
                    }
                    else
                    {
                        _proc.Attributes &= ~ProcAttributes.Instant;
                    }

                    DMCompiler.UnimplementedWarning(statementSet.Location, "set instant is not implemented");
                    break;
                case "background":
                    if (constant.IsTruthy())
                    {
                        _proc.Attributes |= ProcAttributes.Background;
                    }
                    else
                    {
                        _proc.Attributes &= ~ProcAttributes.Background;
                    }
                    break;
                case "name":
                    if (constant is not Expressions.String nameStr) {
                        throw new CompileErrorException(statementSet.Location, "name attribute must be a string");
                    }

                    _proc.VerbName = nameStr.Value;
                    break;
                case "category":
                    _proc.VerbCategory = constant switch {
                        Expressions.String str => str.Value,
                        Expressions.Null => null,
                        _ => throw new CompileErrorException(statementSet.Location, "category attribute must be a string or null")
                    };

                    break;
                case "desc":
                    if (constant is not Expressions.String descStr) {
                        throw new CompileErrorException(statementSet.Location, "desc attribute must be a string");
                    }

                    _proc.VerbDesc = descStr.Value;
                    DMCompiler.UnimplementedWarning(statementSet.Location, "set desc is not implemented");
                    break;
                case "invisibility":
                    // The ref says 0-101 for atoms and 0-100 for verbs
                    // BYOND doesn't clamp the actual var value but it does seem to treat out-of-range values as their extreme
                    if (constant is not Expressions.Number invisNum) {
                        throw new CompileErrorException(statementSet.Location, "invisibility attribute must be an int");
                    }

                    _proc.Invisibility = Convert.ToSByte(Math.Clamp(MathF.Floor(invisNum.Value), 0f, 100f));
                    DMCompiler.UnimplementedWarning(statementSet.Location, "set invisibility is not implemented");
                    break;
                case "src":
                    DMCompiler.UnimplementedWarning(statementSet.Location, "set src is not implemented");
                    break;
            }
        }

        public void ProcessStatementDel(DMASTProcStatementDel statementDel) {
            DMExpression.Emit(_dmObject, _proc, statementDel.Value);
            _proc.DeleteObject();
        }

        public void ProcessStatementSpawn(DMASTProcStatementSpawn statementSpawn) {
            DMExpression.Emit(_dmObject, _proc, statementSpawn.Delay);

            string afterSpawnLabel = _proc.NewLabelName();
            _proc.Spawn(afterSpawnLabel);

            _proc.StartScope();
            {
                ProcessBlockInner(statementSpawn.Body);

                //Prevent the new thread from executing outside its own code
                _proc.PushNull();
                _proc.Return();
            }
            _proc.EndScope();

            _proc.AddLabel(afterSpawnLabel);
        }

        public void ProcessStatementVarDeclaration(DMASTProcStatementVarDeclaration varDeclaration) {
            if (varDeclaration.IsGlobal) { return; } //Currently handled by DMObjectBuilder

            DMExpression value;
            if (varDeclaration.Value != null) {
                try {
                    value = DMExpression.Create(_dmObject, _proc, varDeclaration.Value, varDeclaration.Type);
                } catch (CompileErrorException e) {
                    DMCompiler.Emit(e.Error);
                    value = new Expressions.Null(varDeclaration.Location);
                }
            } else {
                value = new Expressions.Null(varDeclaration.Location);
            }

            bool successful;
            if (varDeclaration.IsConst) {
                if (!value.TryAsConstant(out var constValue)) {
                    DMCompiler.Emit(WarningCode.HardConstContext, varDeclaration.Location, "Const var must be set to a constant");
                    return;
                }

                successful = _proc.TryAddLocalConstVariable(varDeclaration.Name, varDeclaration.Type, constValue);
            } else {
                successful = _proc.TryAddLocalVariable(varDeclaration.Name, varDeclaration.Type);
            }

            if (!successful) {
                DMCompiler.Emit(WarningCode.DuplicateVariable, varDeclaration.Location, $"Duplicate var {varDeclaration.Name}");
                return;
            }

            value.EmitPushValue(_dmObject, _proc);
            _proc.Assign(_proc.GetLocalVariableReference(varDeclaration.Name));
            _proc.Pop();
        }

        public void ProcessStatementReturn(DMASTProcStatementReturn statement) {
            if (statement.Value != null) {
                DMExpression.Emit(_dmObject, _proc, statement.Value);
            } else {
                _proc.PushReferenceValue(DMReference.Self); //Default return value
            }

            _proc.Return();
        }

        public void ProcessStatementIf(DMASTProcStatementIf statement) {
            DMExpression.Emit(_dmObject, _proc, statement.Condition);

            if (statement.ElseBody == null) {
                string endLabel = _proc.NewLabelName();

                _proc.JumpIfFalse(endLabel);
                _proc.StartScope();
                ProcessBlockInner(statement.Body);
                _proc.EndScope();
                _proc.AddLabel(endLabel);
            } else {
                string elseLabel = _proc.NewLabelName();
                string endLabel = _proc.NewLabelName();

                _proc.JumpIfFalse(elseLabel);

                _proc.StartScope();
                ProcessBlockInner(statement.Body);
                _proc.EndScope();
                _proc.Jump(endLabel);

                _proc.AddLabel(elseLabel);
                _proc.StartScope();
                ProcessBlockInner(statement.ElseBody);
                _proc.EndScope();
                _proc.AddLabel(endLabel);
            }
        }

        public void ProcessStatementFor(DMASTProcStatementFor statementFor) {
            _proc.StartScope();
            {
                foreach (var decl in FindVarDecls(statementFor.Expression1)) {
                    ProcessStatementVarDeclaration(new DMASTProcStatementVarDeclaration(statementFor.Location, decl.DeclPath, null));
                }

                var initializer = statementFor.Expression1 != null ? DMExpression.Create(_dmObject, _proc, statementFor.Expression1) : null;

                if (statementFor.Expression2 != null || statementFor.Expression3 != null) {
                    var comparator = statementFor.Expression2 != null ? DMExpression.Create(_dmObject, _proc, statementFor.Expression2) : null;
                    var incrementor = statementFor.Expression3 != null ? DMExpression.Create(_dmObject, _proc, statementFor.Expression3) : null;

                    ProcessStatementForStandard(initializer, comparator, incrementor, statementFor.Body);
                } else {
                    switch (statementFor.Expression1) {
                        case DMASTAssign {Expression: DMASTVarDeclExpression decl, Value: DMASTExpressionInRange range}: {
                            var identifier = new DMASTIdentifier(decl.Location, decl.DeclPath.Path.LastElement);
                            var outputVar = DMExpression.Create(_dmObject, _proc, identifier);

                            var start = DMExpression.Create(_dmObject, _proc, range.StartRange);
                            var end = DMExpression.Create(_dmObject, _proc, range.EndRange);
                            var step = range.Step != null
                                ? DMExpression.Create(_dmObject, _proc, range.Step)
                                : new Number(range.Location, 1);

                            ProcessStatementForRange(initializer, outputVar, start, end, step, statementFor.Body);
                            break;
                        }
                        case DMASTExpressionInRange exprRange: {
                            DMASTVarDeclExpression decl = exprRange.Value as DMASTVarDeclExpression;
                            decl ??= exprRange.Value is DMASTAssign assign
                                ? assign.Expression as DMASTVarDeclExpression
                                : null;

                            DMASTExpression outputExpr;
                            if (decl != null) {
                                outputExpr = new DMASTIdentifier(exprRange.Value.Location, decl.DeclPath.Path.LastElement);
                            } else {
                                outputExpr = exprRange.Value;
                            }

                            var outputVar = DMExpression.Create(_dmObject, _proc, outputExpr);

                            var start = DMExpression.Create(_dmObject, _proc, exprRange.StartRange);
                            var end = DMExpression.Create(_dmObject, _proc, exprRange.EndRange);
                            var step = exprRange.Step != null
                                ? DMExpression.Create(_dmObject, _proc, exprRange.Step)
                                : new Number(exprRange.Location, 1);

                            ProcessStatementForRange(initializer, outputVar, start, end, step, statementFor.Body);
                            break;
                        }
                        case DMASTVarDeclExpression vd: {
                            var declInfo = new ProcVarDeclInfo(vd.DeclPath.Path);
                            var identifier = new DMASTIdentifier(vd.Location, declInfo.VarName);
                            var outputVar = DMExpression.Create(_dmObject, _proc, identifier);

                            ProcessStatementForType(initializer, outputVar, declInfo.TypePath, statementFor.Body);
                            break;
                        }
                        case DMASTExpressionIn exprIn: {
                            DMASTExpression outputExpr;
                            if (exprIn.Value is DMASTVarDeclExpression decl) {
                                outputExpr = new DMASTIdentifier(decl.Location, decl.DeclPath.Path.LastElement);
                            } else {
                                outputExpr = exprIn.Value;
                            }

                            var outputVar = DMExpression.Create(_dmObject, _proc, outputExpr);
                            var list = DMExpression.Create(_dmObject, _proc, exprIn.List);

                            ProcessStatementForList(list, outputVar, statementFor.DMTypes, statementFor.Body);
                            break;
                        }
                        default:
                            DMCompiler.Emit(WarningCode.BadExpression, statementFor.Location, "Invalid expression in for");
                            break;
                    }
                }
            }
            _proc.EndScope();

            IEnumerable<DMASTVarDeclExpression> FindVarDecls(DMASTExpression expr) {
                if (expr is DMASTVarDeclExpression p) {
                    yield return p;
                }
                foreach (var leaf in expr.Leaves()) {
                    foreach(var decl in FindVarDecls(leaf)) {
                        yield return decl;
                    }
                }
            }
        }

        public void ProcessStatementForStandard(DMExpression initializer, DMExpression comparator, DMExpression incrementor, DMASTProcBlockInner body) {
            _proc.StartScope();
            {
                if (initializer != null) {
                    initializer.EmitPushValue(_dmObject, _proc);
                    _proc.Pop();
                }

                string loopLabel = _proc.NewLabelName();
                _proc.LoopStart(loopLabel);
                {
                    if (comparator != null) {
                        comparator.EmitPushValue(_dmObject, _proc);
                        _proc.BreakIfFalse();
                    }

                    ProcessBlockInner(body);

                    _proc.MarkLoopContinue(loopLabel);
                    if (incrementor != null) {
                        incrementor.EmitPushValue(_dmObject, _proc);
                        _proc.Pop();
                    }
                    _proc.LoopJumpToStart(loopLabel);
                }
                _proc.LoopEnd();
            }
            _proc.EndScope();
        }

        public void ProcessStatementForList(DMExpression list, DMExpression outputVar, DMValueType? dmTypes, DMASTProcBlockInner body) {
            if (outputVar is not LValue lValue) {
                DMCompiler.Emit(WarningCode.BadExpression, outputVar.Location, "Invalid output var");
                lValue = null;
            }

            // Depending on the var's type and possibly a given "as [types]", an implicit istype() check is performed
            DreamPath? implicitTypeCheck = null;
            if (dmTypes == null) {
                // No "as" means the var's type will be used
                implicitTypeCheck = lValue?.Path;
            } else if (dmTypes != DMValueType.Anything) {
                // "as anything" performs no check. Other values are unimplemented.
                DMCompiler.UnimplementedWarning(outputVar.Location,
                    $"As type \"{dmTypes}\" in for loops is unimplemented. No type check will be performed.");
            }

            list.EmitPushValue(_dmObject, _proc);
            if (implicitTypeCheck != null) {
                // Create an enumerator that will do the implicit istype() for us
                _proc.CreateFilteredListEnumerator(implicitTypeCheck.Value);
            } else {
                _proc.CreateListEnumerator();
            }

            _proc.StartScope();
            {
                string loopLabel = _proc.NewLabelName();
                _proc.LoopStart(loopLabel);
                {
                    _proc.MarkLoopContinue(loopLabel);

                    if (lValue != null) {
                        (DMReference outputRef, _) = lValue.EmitReference(_dmObject, _proc);
                        _proc.Enumerate(outputRef);
                    }

                    ProcessBlockInner(body);
                    _proc.LoopJumpToStart(loopLabel);
                }
                _proc.LoopEnd();
            }
            _proc.EndScope();
            _proc.DestroyEnumerator();
        }

        public void ProcessStatementForType(DMExpression initializer, DMExpression outputVar, DreamPath? type, DMASTProcBlockInner body) {
            if (type == null) {
                // This shouldn't happen, just to be safe
                DMCompiler.ForcedError(initializer.Location,
                    "Attempted to create a type enumerator with a null type");
                return;
            }

            if (DMObjectTree.TryGetTypeId(type.Value, out var typeId)) {
                _proc.PushType(typeId);
                _proc.CreateTypeEnumerator();
            } else {
                DMCompiler.Emit(WarningCode.ItemDoesntExist, initializer.Location, $"Type {type.Value} does not exist");
            }

            _proc.StartScope();
            {
                if (initializer != null) {
                    initializer.EmitPushValue(_dmObject, _proc);
                    _proc.Pop();
                }

                string loopLabel = _proc.NewLabelName();
                _proc.LoopStart(loopLabel);
                {
                    _proc.MarkLoopContinue(loopLabel);

                    if (outputVar is Expressions.LValue lValue) {
                        (DMReference outputRef, _) = lValue.EmitReference(_dmObject, _proc);
                        _proc.Enumerate(outputRef);
                    } else {
                        DMCompiler.Emit(WarningCode.BadExpression, outputVar.Location, "Invalid output var");
                    }

                    ProcessBlockInner(body);
                    _proc.LoopJumpToStart(loopLabel);
                }
                _proc.LoopEnd();
            }
            _proc.EndScope();
            _proc.DestroyEnumerator();
        }

        public void ProcessStatementForRange(DMExpression initializer, DMExpression outputVar, DMExpression start, DMExpression end, DMExpression step, DMASTProcBlockInner body) {
            start.EmitPushValue(_dmObject, _proc);
            end.EmitPushValue(_dmObject, _proc);
            if (step != null) {
                step.EmitPushValue(_dmObject, _proc);
            } else {
                _proc.PushFloat(1.0f);
            }

            _proc.CreateRangeEnumerator();
            _proc.StartScope();
            {
                if (initializer != null) {
                    initializer.EmitPushValue(_dmObject, _proc);
                    _proc.Pop();
                }

                string loopLabel = _proc.NewLabelName();
                _proc.LoopStart(loopLabel);
                {
                    _proc.MarkLoopContinue(loopLabel);

                    if (outputVar is Expressions.LValue lValue) {
                        (DMReference outputRef, _) = lValue.EmitReference(_dmObject, _proc);
                        _proc.Enumerate(outputRef);
                    } else {
                        DMCompiler.Emit(WarningCode.BadExpression, outputVar.Location, "Invalid output var");
                    }

                    ProcessBlockInner(body);
                    _proc.LoopJumpToStart(loopLabel);
                }
                _proc.LoopEnd();
            }
            _proc.EndScope();
            _proc.DestroyEnumerator();
        }

        //Generic infinite loop, while loops with static expression as their conditional with positive truthfullness get turned into this as well as empty for() calls
        public void ProcessStatementInfLoop(DMASTProcStatementInfLoop statementInfLoop){
            _proc.StartScope();
            {
                string loopLabel = _proc.NewLabelName();
                _proc.LoopStart(loopLabel);
                {
                    _proc.MarkLoopContinue(loopLabel);
                    ProcessBlockInner(statementInfLoop.Body);
                    _proc.LoopJumpToStart(loopLabel);
                }
                _proc.LoopEnd();
            }
            _proc.EndScope();
        }

        public void ProcessStatementWhile(DMASTProcStatementWhile statementWhile) {
            string loopLabel = _proc.NewLabelName();

            _proc.LoopStart(loopLabel);
            {
                _proc.MarkLoopContinue(loopLabel);
                DMExpression.Emit(_dmObject, _proc, statementWhile.Conditional);
                _proc.BreakIfFalse();

                _proc.StartScope();
                {
                    ProcessBlockInner(statementWhile.Body);
                    _proc.LoopJumpToStart(loopLabel);
                }
                _proc.EndScope();
            }
            _proc.LoopEnd();
        }

        public void ProcessStatementDoWhile(DMASTProcStatementDoWhile statementDoWhile) {
            string loopLabel = _proc.NewLabelName();
            string loopEndLabel = _proc.NewLabelName();

            _proc.LoopStart(loopLabel);
            {
                ProcessBlockInner(statementDoWhile.Body);

                _proc.MarkLoopContinue(loopLabel);
                DMExpression.Emit(_dmObject, _proc, statementDoWhile.Conditional);
                _proc.JumpIfFalse(loopEndLabel);
                _proc.LoopJumpToStart(loopLabel);

                _proc.AddLabel(loopEndLabel);
                _proc.Break();
            }
            _proc.LoopEnd();
        }

        public void ProcessStatementSwitch(DMASTProcStatementSwitch statementSwitch) {
            string endLabel = _proc.NewLabelName();
            List<(string CaseLabel, DMASTProcBlockInner CaseBody)> valueCases = new();
            DMASTProcBlockInner defaultCaseBody = null;

            DMExpression.Emit(_dmObject, _proc, statementSwitch.Value);
            foreach (DMASTProcStatementSwitch.SwitchCase switchCase in statementSwitch.Cases) {
                if (switchCase is DMASTProcStatementSwitch.SwitchCaseValues switchCaseValues) {
                    string caseLabel = _proc.NewLabelName();

                    foreach (DMASTExpression value in switchCaseValues.Values) {
                        Constant GetCaseValue(DMASTExpression expression) {
                            Constant constant = null;

                            try {
                                if (!DMExpression.TryConstant(_dmObject, _proc, expression, out constant))
                                    DMCompiler.Emit(WarningCode.HardConstContext, expression.Location, "Expected a constant");
                            } catch (CompileErrorException e) {
                                DMCompiler.Emit(e.Error);
                            }

                            // Return 0 if unsuccessful so that we can continue compiling
                            return constant ?? new Number(expression.Location, 0.0f);
                        }

                        if (value is DMASTSwitchCaseRange range) { // if(1 to 5) or something
                            Constant lower = GetCaseValue(range.RangeStart);
                            Constant upper = GetCaseValue(range.RangeEnd);

                            Constant CoerceBound(Constant bound) {
                                if (bound is Null) { // We do a little null coercion, as a treat
                                    DMCompiler.Emit(WarningCode.MalformedRange, range.RangeStart.Location,
                                        "Malformed range, lower bound is coerced from null to 0");
                                    return new Number(lower.Location, 0.0f);
                                }

                                //DM 514.1580 does NOT care if the constants within a range are strings, and does a strange conversion to 0 or something, without warning or notice.
                                //We are (hopefully) deviating from parity here and just calling that a Compiler error.
                                if (bound is not Number) {
                                    DMCompiler.Emit(WarningCode.InvalidRange, range.RangeStart.Location,
                                        "Invalid range, lower bound is not a number");
                                    bound = new Number(bound.Location, 0.0f);
                                }

                                return bound;
                            }

                            lower = CoerceBound(lower);
                            upper = CoerceBound(upper);

                            lower.EmitPushValue(_dmObject, _proc);
                            upper.EmitPushValue(_dmObject, _proc);
                            _proc.SwitchCaseRange(caseLabel);
                        } else {
                            Constant constant = GetCaseValue(value);

                            constant.EmitPushValue(_dmObject, _proc);
                            _proc.SwitchCase(caseLabel);
                        }
                    }

                    valueCases.Add((caseLabel, switchCase.Body));
                } else {
                    defaultCaseBody = ((DMASTProcStatementSwitch.SwitchCaseDefault)switchCase).Body;
                }
            }
            _proc.Pop();

            if (defaultCaseBody != null) {
                _proc.StartScope();
                {
                    ProcessBlockInner(defaultCaseBody);
                }
                _proc.EndScope();
            }
            _proc.Jump(endLabel);

            foreach ((string CaseLabel, DMASTProcBlockInner CaseBody) valueCase in valueCases) {
                _proc.AddLabel(valueCase.CaseLabel);
                _proc.StartScope();
                {
                    ProcessBlockInner(valueCase.CaseBody);
                }
                _proc.EndScope();
                _proc.Jump(endLabel);
            }

            _proc.AddLabel(endLabel);
        }

        public void ProcessStatementBrowse(DMASTProcStatementBrowse statementBrowse) {
            DMExpression.Emit(_dmObject, _proc, statementBrowse.Receiver);
            DMExpression.Emit(_dmObject, _proc, statementBrowse.Body);
            DMExpression.Emit(_dmObject, _proc, statementBrowse.Options);
            _proc.Browse();
        }

        public void ProcessStatementBrowseResource(DMASTProcStatementBrowseResource statementBrowseResource) {
            DMExpression.Emit(_dmObject, _proc, statementBrowseResource.Receiver);
            DMExpression.Emit(_dmObject, _proc, statementBrowseResource.File);
            DMExpression.Emit(_dmObject, _proc, statementBrowseResource.Filename);
            _proc.BrowseResource();
        }

        public void ProcessStatementOutputControl(DMASTProcStatementOutputControl statementOutputControl) {
            DMExpression.Emit(_dmObject, _proc, statementOutputControl.Receiver);
            DMExpression.Emit(_dmObject, _proc, statementOutputControl.Message);
            DMExpression.Emit(_dmObject, _proc, statementOutputControl.Control);
            _proc.OutputControl();
        }

        public void ProcessStatementOutput(DMASTProcStatementOutput statementOutput) {
            DMExpression left = DMExpression.Create(_dmObject, _proc, statementOutput.A);
            DMExpression right = DMExpression.Create(_dmObject, _proc, statementOutput.B);

            if (left is LValue) {
                // An LValue on the left needs a special opcode so that its reference can be used
                // This allows for special operations like "savefile[...] << ..."

                (DMReference leftRef, _) = left.EmitReference(_dmObject, _proc);
                right.EmitPushValue(_dmObject, _proc);

                _proc.OutputReference(leftRef);
            } else {
                left.EmitPushValue(_dmObject, _proc);
                right.EmitPushValue(_dmObject, _proc);
                _proc.Output();
            }
        }

        public void ProcessStatementInput(DMASTProcStatementInput statementInput) {
            DMExpression left = DMExpression.Create(_dmObject, _proc, statementInput.A);
            DMExpression right = DMExpression.Create(_dmObject, _proc, statementInput.B);

            // The left-side value of an input operation must be an LValue
            // (I think? I haven't found an exception but there could be one)
            if (left is not LValue) {
                DMCompiler.Emit(WarningCode.BadExpression, left.Location, "Left side must be an l-value");
                return;
            }

            // The right side must also be an LValue. Because where else would the value go?
            if (right is not LValue) {
                DMCompiler.Emit(WarningCode.BadExpression, right.Location, "Right side must be an l-value");
                return;
            }

            (DMReference rightRef, _) = right.EmitReference(_dmObject, _proc);
            (DMReference leftRef, _) = left.EmitReference(_dmObject, _proc);

            _proc.Input(leftRef, rightRef);
        }

        public void ProcessStatementTryCatch(DMASTProcStatementTryCatch tryCatch) {
            string catchLabel = _proc.NewLabelName();
            string endLabel = _proc.NewLabelName();

            _proc.StartScope();
            ProcessBlockInner(tryCatch.TryBody);
            _proc.EndScope();
            _proc.Jump(endLabel);

            if (tryCatch.CatchParameter != null)
            {
                //TODO set the value to what is thrown in try
                var param = tryCatch.CatchParameter as DMASTProcStatementVarDeclaration;
                if (!_proc.TryAddLocalVariable(param.Name, param.Type)) {
                    DMCompiler.Emit(WarningCode.DuplicateVariable, param.Location, $"Duplicate var {param.Name}");
                }
            }

            //TODO make catching actually work
            _proc.AddLabel(catchLabel);
            if (tryCatch.CatchBody != null) {
                _proc.StartScope();
                ProcessBlockInner(tryCatch.CatchBody);
                _proc.EndScope();
            }
            _proc.AddLabel(endLabel);

        }

        public void ProcessStatementThrow(DMASTProcStatementThrow statement) {
            //TODO proper value handling and catching

            DMExpression.Emit(_dmObject, _proc, statement.Value);
            _proc.Throw();
        }
    }
}

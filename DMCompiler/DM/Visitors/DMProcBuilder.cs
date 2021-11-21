﻿using OpenDreamShared.Compiler;
using OpenDreamShared.Compiler.DM;
using System.Collections.Generic;
using OpenDreamShared.Dream;
using System;

namespace DMCompiler.DM.Visitors {
    class DMProcBuilder {
        private DMObject _dmObject;
        private DMProc _proc;

        public DMProcBuilder(DMObject dmObject, DMProc proc) {
            _dmObject = dmObject;
            _proc = proc;
        }

        public void ProcessProcDefinition(DMASTProcDefinition procDefinition) {
            if (procDefinition.Body == null) return;

            foreach (DMASTDefinitionParameter parameter in procDefinition.Parameters) {
                string parameterName = parameter.Name;

                if (_proc.HasLocalVariable(parameterName))
                {
                    Program.Error(new CompilerError(null, $"Duplicate argument \"{parameterName}\" on {procDefinition.ObjectPath}/proc/{procDefinition.Name}()"));
                    continue;
                }
                _proc.AddLocalVariable(parameterName, parameter.ObjectType);
                if (parameter.Value != null) { //Parameter has a default value
                    string afterDefaultValueCheck = _proc.NewLabelName();

                    //Don't set parameter to default if not null
                    _proc.PushLocalVariable(parameterName);
                    _proc.IsNull();
                    _proc.JumpIfFalse(afterDefaultValueCheck);

                    //Set default
                    _proc.PushLocalVariable(parameterName);
                    try {
                        DMExpression.Emit(_dmObject, _proc, parameter.Value, parameter.ObjectType);
                    } catch (CompileErrorException e) {
                        Program.Error(e.Error);
                    }
                    _proc.Assign();

                    _proc.AddLabel(afterDefaultValueCheck);
                }
            }

            ProcessBlockInner(procDefinition.Body);
            _proc.ResolveLabels();
        }

        public void ProcessBlockInner(DMASTProcBlockInner block) {
            foreach (DMASTProcStatement statement in block.Statements) {
                try {
                    ProcessStatement(statement);
                } catch (CompileErrorException e) { //Retreat from the statement when there's an error
                    Program.Error(e.Error);
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
                case DMASTProcStatementSet statementSet: ProcessStatementSet(statementSet); break;
                case DMASTProcStatementDel statementDel: ProcessStatementDel(statementDel); break;
                case DMASTProcStatementSpawn statementSpawn: ProcessStatementSpawn(statementSpawn); break;
                case DMASTProcStatementReturn statementReturn: ProcessStatementReturn(statementReturn); break;
                case DMASTProcStatementIf statementIf: ProcessStatementIf(statementIf); break;
                case DMASTProcStatementForStandard statementForStandard: ProcessStatementForStandard(statementForStandard); break;
                case DMASTProcStatementForList statementForList: ProcessStatementForList(statementForList); break;
                case DMASTProcStatementForRange statementForRange: ProcessStatementForRange(statementForRange); break;
                case DMASTProcStatementWhile statementWhile: ProcessStatementWhile(statementWhile); break;
                case DMASTProcStatementDoWhile statementDoWhile: ProcessStatementDoWhile(statementDoWhile); break;
                case DMASTProcStatementSwitch statementSwitch: ProcessStatementSwitch(statementSwitch); break;
                case DMASTProcStatementBrowse statementBrowse: ProcessStatementBrowse(statementBrowse); break;
                case DMASTProcStatementBrowseResource statementBrowseResource: ProcessStatementBrowseResource(statementBrowseResource); break;
                case DMASTProcStatementOutputControl statementOutputControl: ProcessStatementOutputControl(statementOutputControl); break;
                case DMASTProcStatementVarDeclaration varDeclaration: ProcessStatementVarDeclaration(varDeclaration); break;
                case DMASTProcStatementTryCatch tryCatch: ProcessStatementTryCatch(tryCatch); break;
                case DMASTProcStatementThrow dmThrow: ProcessStatementThrow(dmThrow); break;
                case DMASTProcStatementMultipleVarDeclarations multipleVarDeclarations: {
                    foreach (DMASTProcStatementVarDeclaration varDeclaration in multipleVarDeclarations.VarDeclarations) {
                        ProcessStatementVarDeclaration(varDeclaration);
                    }

                    break;
                }
                default: throw new ArgumentException("Invalid proc statement");
            }
        }

        public void ProcessStatementExpression(DMASTProcStatementExpression statement) {
            DMExpression.Emit(_dmObject, _proc, statement.Expression);
            // TODO: does this need pop?
        }

        public void ProcessStatementContinue(DMASTProcStatementContinue statementContinue) {
            _proc.Continue();
        }

        public void ProcessStatementGoto(DMASTProcStatementGoto statementGoto) {
            _proc.Goto(statementGoto.Label.Identifier);
        }

        public void ProcessStatementLabel(DMASTProcStatementLabel statementLabel) {
            _proc.AddLabel(statementLabel.Name + "_codelabel");
        }

        public void ProcessStatementBreak(DMASTProcStatementBreak statementBreak) {
            _proc.Break();
        }

        public void ProcessStatementSet(DMASTProcStatementSet statementSet) {
            //TODO: Proc attributes
            switch (statementSet.Attribute.ToLower()) {
                case "waitfor": {
                    var constant = DMExpression.Constant(_dmObject, _proc, statementSet.Value);

                    if (constant is not Expressions.Number) {
                        throw new CompileErrorException($"waitfor attribute should be a number (got {constant})");
                    }

                    _proc.WaitFor(constant.IsTruthy());
                    break;
                }
                case "opendream_unimplemented": {
                    var constant = DMExpression.Constant(_dmObject, _proc, statementSet.Value);

                    if (constant is not Expressions.Number) {
                        throw new CompileErrorException($"opendream_unimplemented attribute should be a number (got {constant})");
                    }

                    _proc.Unimplemented = constant.IsTruthy();
                    break;
                }
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

            ProcessBlockInner(statementSpawn.Body);
            _proc.Return(); //Prevent the new thread from executing outside its own code

            _proc.AddLabel(afterSpawnLabel);
        }

        public void ProcessStatementVarDeclaration(DMASTProcStatementVarDeclaration varDeclaration) {
            if (varDeclaration.IsGlobal) {
                DMVariable global = _proc.CreateGlobalVariable(varDeclaration.Type, varDeclaration.Name);

                //TODO: Don't set to null if the value can be const-evaluated
                global.Value = new Expressions.Null();
                if (varDeclaration.Value != null) {
                    Expressions.GlobalField field = new(global.Type, _proc.GetGlobalVariableId(global.Name).Value);
                    DMExpression value = DMExpression.Create(_dmObject, _proc, varDeclaration.Value, varDeclaration.Type);
                    Expressions.Assignment assign = new Expressions.Assignment(field, value);

                    DMObjectTree.AddGlobalInitProcAssign(assign);
                }
                
                return;
            }

            _proc.AddLocalVariable(varDeclaration.Name, varDeclaration.Type);

            if (varDeclaration.Value != null) {
                DMExpression.Emit(_dmObject, _proc, varDeclaration.Value, varDeclaration.Type);
            } else {
                _proc.PushNull();
            }

            _proc.SetLocalVariable(varDeclaration.Name);
        }

        public void ProcessStatementReturn(DMASTProcStatementReturn statement) {
            if (statement.Value != null) {
                DMExpression.Emit(_dmObject, _proc, statement.Value);
            } else {
                _proc.PushSelf(); //Default return value
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
                ProcessBlockInner(statement.ElseBody);
                _proc.AddLabel(endLabel);
            }
        }

        public void ProcessStatementForStandard(DMASTProcStatementForStandard statementForStandard) {
            _proc.StartScope();
            {
                if (statementForStandard.Initializer != null) {
                    ProcessStatement(statementForStandard.Initializer);
                }

                string loopLabel = _proc.NewLabelName();
                _proc.LoopStart(loopLabel);
                {
                    DMExpression.Emit(_dmObject, _proc, statementForStandard.Comparator);
                    _proc.BreakIfFalse();

                    ProcessBlockInner(statementForStandard.Body);

                    _proc.LoopContinue(loopLabel);
                    if (statementForStandard.Incrementor != null)
                    {
                        DMExpression.Emit(_dmObject, _proc, statementForStandard.Incrementor);
                    }
                    _proc.LoopJumpToStart(loopLabel);
                }
                _proc.LoopEnd();
            }
            _proc.EndScope();
        }

        public void ProcessStatementForList(DMASTProcStatementForList statementForList) {
            DMExpression.Emit(_dmObject, _proc, statementForList.List);
            _proc.CreateListEnumerator();
            _proc.StartScope();
            {
                if (statementForList.Initializer != null) {
                    ProcessStatement(statementForList.Initializer);
                }

                string loopLabel = _proc.NewLabelName();
                _proc.LoopStart(loopLabel);
                {
                    _proc.Enumerate(statementForList.Variable.Identifier);
                    _proc.BreakIfFalse();

                    DMASTProcStatementVarDeclaration varDeclaration = statementForList.Initializer as DMASTProcStatementVarDeclaration;
                    if (varDeclaration != null && varDeclaration.Type != null)
                    {
                        //This is terrible but temporary
                        //TODO: See https://github.com/wixoaGit/OpenDream/issues/50
                        var obj = DMObjectTree.GetDMObject(varDeclaration.Type.Value);
                        if (statementForList.List is DMASTIdentifier list && list.Identifier == "world" && !obj.IsSubtypeOf(DreamPath.Atom))
                        {
                            var warn = new CompilerWarning(null, "Cannot currently loop 'in world' for non-ATOM types");
                            Program.Warning(warn);
                        }
                        DMExpression.Emit(_dmObject, _proc, statementForList.Variable);
                        _proc.PushPath(varDeclaration.Type.Value);
                        _proc.IsType();

                        _proc.ContinueIfFalse();
                    }

                    ProcessBlockInner(statementForList.Body);

                    _proc.LoopContinue(loopLabel);
                    _proc.LoopJumpToStart(loopLabel);
                }
                _proc.LoopEnd();
            }
            _proc.EndScope();
            _proc.DestroyEnumerator();
        }

        public void ProcessStatementForRange(DMASTProcStatementForRange statementForRange) {
            DMExpression.Emit(_dmObject, _proc, statementForRange.RangeStart);
            DMExpression.Emit(_dmObject, _proc, statementForRange.RangeEnd);
            DMExpression.Emit(_dmObject, _proc, statementForRange.Step);
            _proc.CreateRangeEnumerator();
            _proc.StartScope();
            {
                if (statementForRange.Initializer != null) {
                    ProcessStatement(statementForRange.Initializer);
                }

                string loopLabel = _proc.NewLabelName();
                _proc.LoopStart(loopLabel);
                {
                    _proc.Enumerate(statementForRange.Variable.Identifier);
                    _proc.BreakIfFalse();

                    ProcessBlockInner(statementForRange.Body);

                    _proc.LoopContinue(loopLabel);
                    _proc.LoopJumpToStart(loopLabel);
                }
                _proc.LoopEnd();
            }
            _proc.EndScope();
            _proc.DestroyEnumerator();
        }

        public void ProcessStatementWhile(DMASTProcStatementWhile statementWhile) {
            string loopLabel = _proc.NewLabelName();

            _proc.LoopStart(loopLabel);
            {
                DMExpression.Emit(_dmObject, _proc, statementWhile.Conditional);
                _proc.BreakIfFalse();

                _proc.StartScope();
                {
                    ProcessBlockInner(statementWhile.Body);

                    _proc.LoopContinue(loopLabel);
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

                _proc.LoopContinue(loopLabel);
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
                        if (value is DMASTSwitchCaseRange range) {
                            var lower = DMExpression.Constant(_dmObject, _proc, range.RangeStart);
                            var upper = DMExpression.Constant(_dmObject, _proc, range.RangeEnd);

                            lower.EmitPushValue(_dmObject, _proc);
                            upper.EmitPushValue(_dmObject, _proc);
                            _proc.SwitchCaseRange(caseLabel);
                        } else {
                            var constant = DMExpression.Constant(_dmObject, _proc, value);
                            constant.EmitPushValue(_dmObject, _proc);
                            _proc.SwitchCase(caseLabel);
                        }
                    }

                    valueCases.Add((caseLabel, switchCase.Body));
                } else {
                    defaultCaseBody = ((DMASTProcStatementSwitch.SwitchCaseDefault)switchCase).Body;
                }
            }

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
                _proc.AddLocalVariable(param.Name, param.Type);
            }

            //TODO make catching actually work
            _proc.AddLabel(catchLabel);
            _proc.StartScope();
            ProcessBlockInner(tryCatch.CatchBody);
            _proc.EndScope();
            _proc.AddLabel(endLabel);

        }

        public void ProcessStatementThrow(DMASTProcStatementThrow statement) {
            //TODO proper value handling and catching

            _proc.Throw();
        }
    }
}

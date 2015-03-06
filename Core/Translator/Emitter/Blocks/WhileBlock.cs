﻿using Bridge.Plugin;
using ICSharpCode.NRefactory.CSharp;
using ICSharpCode.NRefactory.TypeSystem;
using System.Collections.Generic;
using System.Linq;

namespace Bridge.NET
{
    public class WhileBlock : AbstractEmitterBlock
    {
        public WhileBlock(IEmitter emitter, WhileStatement whileStatement)
        {
            this.Emitter = emitter;
            this.WhileStatement = whileStatement;
        }

        public WhileStatement WhileStatement 
        { 
            get; 
            set; 
        }

        public override void Emit()
        {
            var awaiters = this.Emitter.IsAsync ? this.GetAwaiters(this.WhileStatement) : null;

            if (awaiters != null && awaiters.Length > 0)
            {
                this.VisitAsyncWhileStatement();
            }
            else
            {
                this.VisitWhileStatement();
            }
        }

        protected void VisitAsyncWhileStatement()
        {
            var oldValue = this.Emitter.ReplaceAwaiterByVar;
            var jumpStatements = this.Emitter.JumpStatements;
            this.Emitter.JumpStatements = new List<IJumpInfo>();            
            
            IAsyncStep conditionStep = null;
            var lastStep = this.Emitter.AsyncBlock.Steps.Last();
            if (string.IsNullOrWhiteSpace(lastStep.Output.ToString()))
            {
                conditionStep = lastStep;
            }
            else
            {
                lastStep.JumpToStep = this.Emitter.AsyncBlock.Step;
                conditionStep = this.Emitter.AsyncBlock.AddAsyncStep();
            }

            this.WriteAwaiters(this.WhileStatement.Condition);
            this.Emitter.ReplaceAwaiterByVar = true;

            this.WriteIf();
            this.WriteOpenParentheses(true);
            this.WhileStatement.Condition.AcceptVisitor(this.Emitter);
            this.WriteCloseParentheses(true);
            this.Emitter.ReplaceAwaiterByVar = oldValue;

            this.WriteSpace();
            this.BeginBlock();

            var writer = this.SaveWriter();
            this.Emitter.IgnoreBlock = this.WhileStatement.EmbeddedStatement;
            var startCount = this.Emitter.AsyncBlock.Steps.Count;
            this.WhileStatement.EmbeddedStatement.AcceptVisitor(this.Emitter);

            if (!AbstractEmitterBlock.IsJumpStatementLast(this.Emitter.Output.ToString()))
            {
                this.WriteNewLine();
                this.Write("$step = " + conditionStep.Step + ";");
                this.WriteNewLine();
                this.Write("continue;");
            }     

            this.RestoreWriter(writer);

            this.WriteNewLine();
            this.EndBlock();
            this.WriteSpace();

            if (!AbstractEmitterBlock.IsJumpStatementLast(this.Emitter.Output.ToString()))
            {
                this.WriteNewLine();
                this.Write("$step = " + this.Emitter.AsyncBlock.Step + ";");
                this.WriteNewLine();
                this.Write("continue;");
            }           

            var nextStep = this.Emitter.AsyncBlock.AddAsyncStep();
            conditionStep.JumpToStep = nextStep.Step;

            if (this.Emitter.JumpStatements.Count > 0)
            {
                foreach (var jump in this.Emitter.JumpStatements)
                {
                    jump.Output.Insert(jump.Position, jump.Break ? nextStep.Step : conditionStep.Step);
                }
            }

            this.Emitter.JumpStatements = jumpStatements;
        }

        protected void VisitWhileStatement()
        {
            this.WriteWhile();
            this.WriteOpenParentheses();
            this.WhileStatement.Condition.AcceptVisitor(this.Emitter);
            this.WriteOpenCloseParentheses();
            this.EmitBlockOrIndentedLine(this.WhileStatement.EmbeddedStatement);
        }
    }
}
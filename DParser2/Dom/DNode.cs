using System.Collections.Generic;
using D_Parser.Dom.Expressions;
using D_Parser.Parser;

namespace D_Parser.Dom
{
    public abstract class DNode :AbstractNode
    {
        public ITemplateParameter[] TemplateParameters=null; // Functions, Templates
		public IExpression TemplateConstraint;

		public bool ContainsTemplateParameter(string Name)
		{
			if (TemplateParameters != null)
				foreach (var tp in TemplateParameters)
					if (tp.Name == Name)
						return true;

			return false;
		}

		public IEnumerable<TemplateParameterNode> TemplateParameterNodes
		{
			get {
				if (TemplateParameters != null)
					foreach (var p in TemplateParameters)
						yield return new TemplateParameterNode(p) { Owner=this };
			}
		}
		
        public List<DAttribute> Attributes = new List<DAttribute>();
        public bool ContainsAttribute(params int[] Token)
        {
            return DAttribute.ContainsAttribute(Attributes, Token);
        }

		public bool ContainsPropertyAttribute(string prop="property")
		{
			foreach (var attr in Attributes)
				if (attr.IsProperty && attr.LiteralContent is string && attr.LiteralContent.ToString() == prop)
					return true;
			return false;
		}

        public override void AssignFrom(INode other)
        {
			if (other is DNode)
			{
				TemplateParameters = (other as DNode).TemplateParameters;
				Attributes = (other as DNode).Attributes;
			}
			base.AssignFrom(other);
        }

        public string AttributeString
        {
            get
            {
                string s = "";
                foreach (var attr in Attributes)
					if(attr!=null)
						s += attr.ToString() + " ";
                return s.Trim();
            }
        }

        /// <summary>
        /// Returns attributes, type and name combined to one string
        /// </summary>
        /// <returns></returns>
        public override string ToString(bool Attributes,bool IncludePath)
        {
			string s = ""; 
				
			if(Attributes)
				s=AttributeString+" ";

			s += base.ToString(Attributes,IncludePath);

            // Template parameters
            if (TemplateParameters!=null && TemplateParameters.Length > 0)
            {
				if (this is DVariable)
					s += '!';

                s += "(";
				foreach (var p in TemplateParameters)
					s += p.ToString() + ",";

                s = s.Trim(',')+ ")";
            }
            
            return s.Trim();
        }

		public bool IsClassMember
		{
			get
			{
				return Parent is DClassLike && ((DClassLike)Parent).ClassType == DTokens.Class;
			}
		}

        public bool IsPublic
        {
            get
            {
                return !ContainsAttribute(DTokens.Private, DTokens.Protected);
            }
        }

        public bool IsStatic
        {
            get
            {
                return ContainsAttribute(DTokens.Static);
            }
        }
	}
}

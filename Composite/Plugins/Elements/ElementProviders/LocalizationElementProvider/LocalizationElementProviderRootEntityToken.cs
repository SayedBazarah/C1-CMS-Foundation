﻿using Composite.C1Console.Security;
using Composite.C1Console.Security.SecurityAncestorProviders;


namespace Composite.Plugins.Elements.ElementProviders.LocalizationElementProvider
{
    [SecurityAncestorProvider(typeof(NoAncestorSecurityAncestorProvider))]
	internal sealed class LocalizationElementProviderRootEntityToken : EntityToken
	{
        public override string Type
        {
            get { return ""; }
        }

        public override string Source
        {
            get { return ""; }
        }

        public override string Id
        {
            get { return "LocalizationElementProviderRootEntityToken"; }
        }

        public override string Serialize()
        {
            return DoSerialize();
        }

        public static EntityToken Deserialize(string serializedEntityToken)
        {
            return new LocalizationElementProviderRootEntityToken();
        }
    }
}

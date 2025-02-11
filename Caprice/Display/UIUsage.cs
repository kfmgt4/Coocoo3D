﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Reflection;

namespace Caprice.Display
{
    public class UIUsage
    {
        public MemberInfo MemberInfo;

        public string Name;

        public string Description;

        public UIShowType UIShowType;

        public UIColorAttribute colorAttribute;

        public UISliderAttribute sliderAttribute;

        public UIDragFloatAttribute dragFloatAttribute;

        public UIDragIntAttribute dragIntAttribute;

        public UITreeAttribute treeAttribute;
    }
}

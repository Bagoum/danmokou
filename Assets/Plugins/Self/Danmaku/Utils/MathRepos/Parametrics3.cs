using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using UnityEngine;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using Ex = System.Linq.Expressions.Expression;
using ExTP = System.Func<DMath.TExPI, TEx<UnityEngine.Vector2>>;
using ExTP3 = System.Func<DMath.TExPI, TEx<UnityEngine.Vector3>>;
using ExFXY = System.Func<TEx<float>, TEx<float>>;
using ExBPY = System.Func<DMath.TExPI, TEx<float>>;
using ExPred = System.Func<DMath.TExPI, TEx<bool>>;
using static DMath.ExMHelpers;
using static ExUtils;
using FR = DMath.FXYRepo;
using static DMath.ExM;
using B = DMath.BPYRepo;

namespace DMath {
/// <summary>
/// Functions that return V3.
/// </summary>
[SuppressMessage("ReSharper", "MemberCanBePrivate.Global")]
[SuppressMessage("ReSharper", "UnusedMember.Global")]
public static partial class Parametrics3 {
    /// <summary>
    /// Assign local variables that can be repeatedly used without reexecution. Shortcut: ::
    /// </summary>
    /// <param name="aliases">List of each variable and its assigned float value</param>
    /// <param name="inner">Code to execute within the scope of the variables</param>
    public static ExTP3 LetFloats((string, ExBPY)[] aliases, ExTP3 inner) => bpi => ReflectEx.Let(aliases, () => inner(bpi), bpi);
    /// <summary>
    /// Assign local variables that can be repeatedly used without reexecution. Shortcut: ::v2
    /// </summary>
    /// <param name="aliases">List of each variable and its assigned vector value</param>
    /// <param name="inner">Code to execute within the scope of the variables</param>
    public static ExTP3 LetV2s((string, ExTP)[] aliases, ExTP3 inner) => bpi => ReflectEx.Let(aliases, () => inner(bpi), bpi);
    /// <summary>
    /// Reference a v2 value defined in a let function. Shortcut: &amp;
    /// </summary>
    /// <returns></returns>
    public static ExTP3 Reference(string alias) => TP(ReflectEx.ReferenceLetBPI<Vector2>(alias));

    /// <summary>
    /// Derive a parametric3 equation from a parametric2 function (Z is set to zero)
    /// </summary>
    /// <param name="tp">Parametric function to assign to x,y components</param>
    /// <returns></returns>
    [Fallthrough(50)]
    public static ExTP3 TP(ExTP tp) => bpi => ((Expression) tp(bpi)).As<Vector3>();

    [Fallthrough(40)]
    public static ExTP3 Circ(CCircle c) => bpi => ExC((Vector3) c);
    
    /// <summary>
    /// Generate an offset for a wing pattern. Requires the follow bindings:
    /// <br/>px = index along wing span
    /// <br/>py = index along feather
    /// <br/>plr = +-1 for left/right (1 = right wing (visible left))
    /// </summary>
    /// <param name="wx">Length along wing span</param>
    /// <param name="wy">Length along feather</param>
    /// <param name="per">Period of movement</param>
    /// <returns></returns>
    /// TODO WARNING if you throw variables into code like this, you MUST use FormattableString.Invariant! The SM parser will not recognize decimal commas!
    public static ExTP3 Wings1(float wx, float wy, float per) => 
	    FormattableString.Invariant(FormattableStringFactory.Create(@":: {{
	pxr	  / &px {0}
	pyr	  / &py {1} " +
	//Farther away feathers have longer distance between bones
@" xs    * -1 + 2.5 cosine(3.5, 2, - 0.9 &pxr)
	sw	  swing2 0.35 {2} 0 0.8 1 (- t * 0.18 &pxr)
	swc   c &sw
	sws	  swing2 0.55 {2} 1.03 1 0.9 (+ * 0.5 {2} - t * 0.18 &pxr)
}} " +
@"       multiplyx &plr
			qrotate py 45 " +  //pushes wing towards spine
@"           + cxy -0.5 0.6
			 cylinderwrap 
				+ 3 * 4 &sw " + //When wings are open, rotate around larger diameter
				"* / pi -1.55 &swc " + //When wings are closed, negative lean allows more pivoted rotation. / pi -1 is super rot
				"* 0.8 &swc " + //Only perform rotation when wings are closed
				"+ -1.3 * 0.9 &swc " + //Start (swc=0) by rotating downwards, then close (swc) by rotating inwards
    //Raw wing position equation (relative to wing anchor)
@"              + pxy
                    * -0.13 &px
                    * 0.1 * {0} ^(&pxr, .7)
                  * " + 
    //Removed this dynamic effect: * &sws + 1 sine 0.4 0.04 - t &pyr 
@"                 &sws 
			        rxy " + 
    //Closer feathers are rotated inwards (this is the visible left wing)
@"                     *c 73 &pxr
			            * 0.06 * &xs &py " +
    //Feathers rotate inwards (^-(&pyr, ...) < 0 when &pyr < 1) and then back into normal position, more so at farther feathers
			            "* 1.5 ^-(&pyr, + 1.1 * 0.5 &pxr)", wx, wy, per)).Into<ExTP3>();

    /// <summary>
    /// Generate an offset for a wing pattern. Requires the following bindings:
    /// <br/>px = index along wing span
    /// <br/>py = index along feather
    /// <br/>plr = +-1 for left/right (1 = left wing (visible right))
    /// </summary>
    /// <param name="wx"></param>
    /// <param name="wy"></param>
    /// <param name="per"></param>
    /// <returns></returns>
    /// TODO WARNING if you throw variables into code like this, you MUST use FormattableString.Invariant! The SM parser will not recognize decimal commas!
    public static ExTP3 Wings2(float wx, float wy, float per) => FormattableString.Invariant($@":: {{
	pxr	/ &px {wx}
	pyr	/ &py {wy}
	swa	 * (linear 0.9 0.4 &pxr) * (linear 0.9 0.3 &pyr) swing2 0.35 {per} -80 35 50 (+ t * -0.2 &pxr)
	sws	 swing2 0.55 {per} 1.03 1 0.85 (+ {per/2} - t * 0.2 &pxr)
}} multiplyx &plr +
		cxy 0.3 0.2
		rxy
			&swa
			+ 0.2 * &sws * opacity 0.4 &pyr * 0.1 &px 
			* 0.3 * opacity 0.9 &pxr &py
").Into<ExTP3>();

}

}
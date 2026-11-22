import SwiftUI
import AppKit
struct GradientEditorView:View {
 @Binding var gradient:GradientSpec
 var gesture:(Bool)->Void={_ in}
 var body:some View{DisclosureGroup(L10n.text("Gradiente")){VStack(alignment:.leading){Toggle(L10n.text("Activar gradiente"),isOn:$gradient.enabled);ColorPicker(L10n.text("Segundo color"),selection:Binding(get:{Color(nsColor:NSColor(hex:gradient.end))},set:{gradient.end=NSColor($0).hex}),supportsOpacity:true).disabled(!gradient.enabled);Text(L10n.text("Dirección del gradiente"));Slider(value:$gradient.angle,in:0...360,step:1,onEditingChanged:gesture).disabled(!gradient.enabled)}}}
}
struct ColorPropertyView:View {
 let title:String
 @Binding var color:String
 @Binding var gradient:GradientSpec
 var supportsOpacity=true
 var gesture:(Bool)->Void={_ in}
 var body:some View{VStack(alignment:.leading){ColorPicker(L10n.text(title),selection:Binding(get:{Color(nsColor:NSColor(hex:color))},set:{color=NSColor($0).hex}),supportsOpacity:supportsOpacity);GradientEditorView(gradient:$gradient,gesture:gesture)}}
}

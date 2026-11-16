import AppKit
import SwiftUI

/// Native row dragging, insertion gaps and auto-scrolling; style edits never reload the list.
final class LayerScrollView:NSScrollView {
 override func tile(){super.tile();guard let table=documentView as? NSTableView else{return};let width=contentSize.width;guard width>0 else{return};if abs(table.frame.width-width)>0.5{table.setFrameSize(NSSize(width:width,height:table.frame.height))};if let column=table.tableColumns.first,abs(column.width-width)>0.5{column.width=width}}
}
struct LayerTable:NSViewRepresentable {
 @ObservedObject var model:SceneEditorModel
 func makeCoordinator()->Coordinator{Coordinator(model)}
 func makeNSView(context:Context)->NSScrollView {
  let table=NSTableView(frame:NSRect(x:0,y:0,width:290,height:180));table.autoresizingMask=[.width];let column=NSTableColumn(identifier:.init("layer"));column.width=260;table.addTableColumn(column);table.headerView=nil;table.rowHeight=34;table.intercellSpacing=NSSize(width:0,height:2);table.columnAutoresizingStyle = .lastColumnOnlyAutoresizingStyle;table.backgroundColor = .clear;table.focusRingType = .none;table.allowsMultipleSelection=false
  table.delegate=context.coordinator;table.dataSource=context.coordinator;table.registerForDraggedTypes([Coordinator.format]);table.setDraggingSourceOperationMask(.move,forLocal:true);table.draggingDestinationFeedbackStyle = .gap
  let scroll=LayerScrollView();scroll.documentView=table;scroll.hasVerticalScroller=true;scroll.drawsBackground=false;context.coordinator.table=table;context.coordinator.sync();return scroll
 }
 func updateNSView(_ view:NSScrollView,context:Context){context.coordinator.sync()}
 final class Coordinator:NSObject,NSTableViewDataSource,NSTableViewDelegate {
  static let format=NSPasteboard.PasteboardType("dev.wirepulse.layer")
  struct Row:Equatable{var id:String,name:String;var visible:Bool,locked:Bool}
  let model:SceneEditorModel;weak var table:NSTableView?;var rows=[Row]();var updating=false
  init(_ model:SceneEditorModel){self.model=model}
  func sync(){guard let table=table else{return};updating=true;defer{updating=false};let next=model.document.items.reversed().map{Row(id:$0.id,name:$0.name,visible:$0.visible,locked:$0.locked)}
   if next != rows{rows=next;table.reloadData()}
   let selected=rows.firstIndex{$0.id==model.selected} ?? -1
   if table.selectedRow != selected{if selected>=0{table.selectRowIndexes(IndexSet(integer:selected),byExtendingSelection:false)}else{table.deselectAll(nil)}}
  }
  func numberOfRows(in tableView:NSTableView)->Int{rows.count}
  func tableView(_ tableView:NSTableView,viewFor tableColumn:NSTableColumn?,row:Int)->NSView? {
   let data=rows[row],cell=NSTableCellView();let label=NSTextField(labelWithString:"⠿  "+(data.visible ? "● ":"◌ ")+data.name);label.lineBreakMode = .byTruncatingTail;label.translatesAutoresizingMaskIntoConstraints=false;cell.addSubview(label);cell.textField=label
   let lock=NSButton(image:NSImage(systemSymbolName:data.locked ? "lock.fill":"lock.open",accessibilityDescription:data.locked ? "Desbloquear "+data.name:"Bloquear "+data.name)!,target:self,action:#selector(toggleLock(_:)));lock.setAccessibilityLabel((data.locked ? "Desbloquear ":"Bloquear ")+data.name);lock.identifier = .init(data.id);lock.bezelStyle = .inline;lock.isBordered=false;lock.toolTip=data.locked ? "Desbloquear objeto":"Bloquear objeto";lock.translatesAutoresizingMaskIntoConstraints=false;cell.addSubview(lock)
   NSLayoutConstraint.activate([label.leadingAnchor.constraint(equalTo:cell.leadingAnchor,constant:6),label.centerYAnchor.constraint(equalTo:cell.centerYAnchor),label.trailingAnchor.constraint(equalTo:lock.leadingAnchor,constant:-6),lock.trailingAnchor.constraint(equalTo:cell.trailingAnchor,constant:-6),lock.centerYAnchor.constraint(equalTo:cell.centerYAnchor),lock.widthAnchor.constraint(equalToConstant:24)])
   return cell
  }
  @objc func toggleLock(_ sender:NSButton){guard let id=sender.identifier?.rawValue else{return};model.selected=id;model.change{$0.locked.toggle()}}
  func tableViewSelectionDidChange(_ notification:Notification){guard !updating,let table=table else{return};let index=table.selectedRow;let id=rows.indices.contains(index) ? rows[index].id:nil;if model.selected != id{model.selected=id}}
  func tableView(_ tableView:NSTableView,pasteboardWriterForRow row:Int)->NSPasteboardWriting?{guard rows.indices.contains(row) else{return nil};let item=NSPasteboardItem();item.setString(rows[row].id,forType:Self.format);return item}
  func tableView(_ tableView:NSTableView,validateDrop info:NSDraggingInfo,proposedRow row:Int,proposedDropOperation operation:NSTableView.DropOperation)->NSDragOperation{guard info.draggingSource as? NSTableView === tableView,info.draggingPasteboard.string(forType:Self.format) != nil else{return []};tableView.setDropRow(row,dropOperation:.above);return .move}
  func tableView(_ tableView:NSTableView,acceptDrop info:NSDraggingInfo,row:Int,dropOperation:NSTableView.DropOperation)->Bool{guard let source=info.draggingPasteboard.string(forType:Self.format),!rows.isEmpty else{return false};if row>=rows.count{model.reorder(source,rows.last!.id,false)}else{model.reorder(source,rows[max(0,row)].id,true)};sync();return true}
 }
}

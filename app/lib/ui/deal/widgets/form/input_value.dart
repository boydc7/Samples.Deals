import 'package:flutter/material.dart';
import 'package:rxdart/subjects.dart';
import 'package:rydr_app/models/enums/deal.dart';
import 'package:rydr_app/ui/deal/widgets/shared/text_field.dart';

class DealInputValue extends StatefulWidget {
  final BehaviorSubject<double> valueStream;
  final Stream<bool> focusStream;
  final Function handleUpdate;
  final Function handleUpdateFocus;
  final bool isExpired;
  final DealType dealType;

  DealInputValue({
    @required this.valueStream,
    @required this.focusStream,
    @required this.handleUpdate,
    @required this.handleUpdateFocus,
    this.isExpired = false,
    this.dealType = DealType.Deal,
  });

  @override
  _DealInputValueState createState() => _DealInputValueState();
}

class _DealInputValueState extends State<DealInputValue> {
  final FocusNode focusNode = FocusNode();
  TextEditingController controller;

  @override
  void initState() {
    super.initState();

    controller = TextEditingController(
        text: widget.valueStream.value != null
            ? widget.valueStream.value.toString()
            : '');

    controller.addListener(() {
      if (controller.text.trim().length > 0 &&
          double.tryParse(controller.text.trim()) != null) {
        widget.handleUpdate(double.parse(controller.text.trim()).toDouble());
      } else {
        widget.handleUpdate(null);
      }
    });

    focusNode.addListener(() {
      widget.handleUpdateFocus(focusNode.hasFocus);
    });
  }

  @override
  void dispose() {
    controller.dispose();
    focusNode.dispose();

    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    return widget.isExpired
        ? ListTile(
            title: Text(
              'Estimated Cost of Goods',
              style: Theme.of(context).textTheme.bodyText2,
            ),
            trailing: Text(widget.valueStream.value != null
                ? ("\$" + widget.valueStream.value.toStringAsFixed(2))
                : ''),
          )
        : Padding(
            padding: EdgeInsets.only(left: 16, right: 16, bottom: 16),
            child: Column(
              children: <Widget>[
                DealTextField(
                  isVirtual: widget.dealType == DealType.Virtual,
                  controller: controller,
                  focusNode: focusNode,
                  minLines: 1,
                  maxLines: 1,
                  maxCharacters: 6,
                  labelText: 'Estimated Cost of Goods*',
                  hintText: 'Not visible to the creator',
                  keyboardType: TextInputType.numberWithOptions(decimal: true),
                ),
                StreamBuilder<bool>(
                    stream: widget.focusStream,
                    builder: (context, snapshot) {
                      return AnimatedContainer(
                        duration: Duration(milliseconds: 250),
                        height: snapshot.data != null && snapshot.data == true
                            ? 32.0
                            : 0.0,
                        child: Container(
                          padding: EdgeInsets.only(top: 4.0),
                          alignment: Alignment.centerRight,
                          child: Text(
                            'Total cost to the business in goods or time for one RYDR.',
                            style: Theme.of(context).textTheme.caption.merge(
                                  TextStyle(
                                    color: Theme.of(context).brightness ==
                                            Brightness.dark
                                        ? Theme.of(context)
                                            .textTheme
                                            .bodyText2
                                            .color
                                        : Theme.of(context).primaryColor,
                                  ),
                                ),
                          ),
                        ),
                      );
                    }),
              ],
            ),
          );
  }
}

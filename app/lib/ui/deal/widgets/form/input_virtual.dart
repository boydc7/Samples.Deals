import 'package:flutter/material.dart';
import 'package:rydr_app/models/enums/deal.dart';
import 'package:rydr_app/models/place.dart';
import 'package:rydr_app/ui/deal/blocs/add_deal.dart';

class DealInputVirtual extends StatelessWidget {
  final Stream<DealType> valueStream;
  final DealAddBloc bloc;

  DealInputVirtual({
    @required this.valueStream,
    this.bloc,
  });

  @override
  Widget build(BuildContext context) => StreamBuilder<DealType>(
      stream: valueStream,
      builder: (context, snapshot) {
        if (snapshot.connectionState != ConnectionState.waiting) {
          return Container(
            decoration: BoxDecoration(
              borderRadius: BorderRadius.circular(6),
              border: Border.all(
                  color: snapshot.data == DealType.Virtual
                      ? Colors.deepOrange
                      : Theme.of(context).primaryColor,
                  width: 2),
              color: Theme.of(context).appBarTheme.color,
            ),
            margin: EdgeInsets.only(left: 16, right: 16, bottom: 32),
            child: Stack(
              children: <Widget>[
                AnimatedPositioned(
                    left: snapshot.data == DealType.Virtual
                        ? MediaQuery.of(context).size.width / 2 - 14
                        : 4,
                    top: 4,
                    child: AnimatedContainer(
                      curve: Curves.easeInOut,
                      duration: Duration(milliseconds: 250),
                      height: kMinInteractiveDimension * 2.5 - 8,
                      width: MediaQuery.of(context).size.width / 2 - 26,
                      decoration: BoxDecoration(
                        borderRadius: BorderRadius.only(
                          topLeft: Radius.circular(
                              snapshot.data == DealType.Virtual ? 0 : 2),
                          topRight: Radius.circular(
                              snapshot.data == DealType.Virtual ? 2 : 0),
                          bottomLeft: Radius.circular(
                              snapshot.data == DealType.Virtual ? 0 : 2),
                          bottomRight: Radius.circular(
                            snapshot.data == DealType.Virtual ? 2 : 0,
                          ),
                        ),
                        color: snapshot.data == DealType.Virtual
                            ? Colors.deepOrange
                            : Theme.of(context).primaryColor,
                      ),
                    ),
                    curve: Curves.easeInOut,
                    duration: Duration(milliseconds: 250)),
                Row(
                  children: <Widget>[
                    Expanded(
                      child: InkWell(
                        onTap: () => bloc.setDealType(DealType.Deal),
                        child: Container(
                          height: kMinInteractiveDimension * 2.5,
                          padding: EdgeInsets.symmetric(horizontal: 16),
                          child: Column(
                            mainAxisAlignment: MainAxisAlignment.center,
                            crossAxisAlignment: CrossAxisAlignment.center,
                            children: <Widget>[
                              Text(
                                "In-Person",
                                style: Theme.of(context)
                                    .textTheme
                                    .bodyText1
                                    .merge(
                                      TextStyle(
                                        fontWeight:
                                            snapshot.data == DealType.Deal
                                                ? FontWeight.bold
                                                : FontWeight.w500,
                                        color: snapshot.data == DealType.Deal
                                            ? Theme.of(context)
                                                .scaffoldBackgroundColor
                                            : Theme.of(context)
                                                .textTheme
                                                .bodyText2
                                                .color,
                                      ),
                                    ),
                              ),
                              SizedBox(height: 4),
                              StreamBuilder<Place>(
                                  stream: bloc.place,
                                  builder: (context, place) {
                                    return Text(
                                      "To be redeemed at\n${place.data?.name}",
                                      textAlign: TextAlign.center,
                                      style: Theme.of(context)
                                          .textTheme
                                          .caption
                                          .merge(
                                            TextStyle(
                                              fontWeight:
                                                  snapshot.data == DealType.Deal
                                                      ? FontWeight.w500
                                                      : FontWeight.normal,
                                              color: snapshot.data ==
                                                      DealType.Deal
                                                  ? Theme.of(context)
                                                      .scaffoldBackgroundColor
                                                  : Theme.of(context).hintColor,
                                            ),
                                          ),
                                    );
                                  })
                            ],
                          ),
                        ),
                      ),
                    ),
                    Expanded(
                      child: InkWell(
                        onTap: () => bloc.setDealType(DealType.Virtual),
                        child: Container(
                          height: kMinInteractiveDimension * 2.5,
                          padding: EdgeInsets.symmetric(horizontal: 16),
                          child: Column(
                            mainAxisAlignment: MainAxisAlignment.center,
                            crossAxisAlignment: CrossAxisAlignment.center,
                            children: <Widget>[
                              Text(
                                "Virtual",
                                style: Theme.of(context)
                                    .textTheme
                                    .bodyText1
                                    .merge(
                                      TextStyle(
                                        fontWeight:
                                            snapshot.data == DealType.Virtual
                                                ? FontWeight.bold
                                                : FontWeight.w500,
                                        color: snapshot.data == DealType.Virtual
                                            ? Theme.of(context)
                                                .scaffoldBackgroundColor
                                            : Theme.of(context)
                                                .textTheme
                                                .bodyText2
                                                .color,
                                      ),
                                    ),
                              ),
                              SizedBox(height: 4),
                              Text(
                                "To be redeemed and completed online",
                                textAlign: TextAlign.center,
                                style: Theme.of(context)
                                    .textTheme
                                    .caption
                                    .merge(
                                      TextStyle(
                                        fontWeight:
                                            snapshot.data == DealType.Virtual
                                                ? FontWeight.w500
                                                : FontWeight.normal,
                                        color: snapshot.data == DealType.Virtual
                                            ? Theme.of(context)
                                                .scaffoldBackgroundColor
                                            : Theme.of(context).hintColor,
                                      ),
                                    ),
                              )
                            ],
                          ),
                        ),
                      ),
                    )
                  ],
                ),
              ],
            ),
          );
        } else {
          return Container(height: 80);
        }
      });
}

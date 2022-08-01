import 'package:flutter/material.dart';
import 'package:rydr_app/app/theme.dart';
import 'package:rydr_app/app/routing.dart';
import 'package:rydr_app/app/utils.dart';
import 'package:rydr_app/models/deal.dart';
import 'package:rydr_app/models/enums/deal.dart';
import 'package:rydr_app/models/list_page_arguments.dart';

/// Thid widget displays atop a deal for when a business is viewing their deals in the marketplace
/// it has the four, sometimes five circles which show "Requests", "Invites", "In Progress", "Redeemed" and "Completed"
/// the are clickable and will take the business to list of requests for the given deal and request status
class DealMetrics extends StatelessWidget {
  final Deal deal;

  DealMetrics(this.deal);

  void onTap(BuildContext context, DealRequestStatus status) =>
      Navigator.of(context).pushNamed(
        AppRouting.getRequestsPending,
        arguments: ListPageArguments(
          layoutType: ListPageLayout.StandAlone,
          filterDealId: deal.id,
          filterDealName: deal.title,
          filterRequestStatus: [status],
        ),
      );

  @override
  Widget build(BuildContext context) {
    final bool dark = Theme.of(context).brightness == Brightness.dark;
    final int invited = deal.getStat(DealStatType.currentInvites);
    final int requested = deal.getStat(DealStatType.currentRequested);
    final int inProgress = deal.getStat(DealStatType.currentApproved);
    final int redeemed = deal.getStat(DealStatType.currentRedeemed);
    final int completed = deal.getStat(DealStatType.currentCompleted);

    return Padding(
      padding: EdgeInsets.only(
        left: invited > 0 ? 16 : 32,
        right: invited > 0 ? 16 : 32,
        bottom: 16,
      ),
      child: Stack(
        overflow: Overflow.visible,
        alignment: Alignment.topCenter,
        children: <Widget>[
          Positioned(
            top: 24,
            left: invited > 0 ? 32 : 64,
            child: Container(
              width: invited > 0
                  ? MediaQuery.of(context).size.width - 128
                  : MediaQuery.of(context).size.width - 192,
              height: 1.5,
              color: Theme.of(context).canvasColor,
            ),
          ),
          Column(
            mainAxisAlignment: MainAxisAlignment.center,
            children: <Widget>[
              Row(
                mainAxisAlignment: MainAxisAlignment.spaceBetween,
                children: <Widget>[
                  _buildStatCircle(
                    context,
                    () => onTap(context, DealRequestStatus.requested),
                    Utils.getRequestStatusColor(
                        DealRequestStatus.requested, dark),
                    requested,
                  ),
                  invited > 0
                      ? _buildStatCircle(
                          context,
                          () => onTap(context, DealRequestStatus.invited),
                          Utils.getRequestStatusColor(
                              DealRequestStatus.invited, dark),
                          invited,
                        )
                      : Container(),
                  _buildStatCircle(
                    context,
                    () => onTap(context, DealRequestStatus.inProgress),
                    Utils.getRequestStatusColor(
                        DealRequestStatus.inProgress, dark),
                    inProgress,
                  ),
                  _buildStatCircle(
                    context,
                    () => onTap(context, DealRequestStatus.redeemed),
                    Utils.getRequestStatusColor(
                        DealRequestStatus.redeemed, dark),
                    redeemed,
                  ),
                  _buildStatCircle(
                    context,
                    () => onTap(context, DealRequestStatus.completed),
                    Utils.getRequestStatusColor(
                        DealRequestStatus.completed, dark),
                    completed,
                  ),
                ],
              ),
              SizedBox(height: 8.0),
              Row(
                mainAxisAlignment: MainAxisAlignment.spaceBetween,
                crossAxisAlignment: CrossAxisAlignment.start,
                children: <Widget>[
                  _buildLabel(context, 'Requests'),
                  invited > 0 ? _buildLabel(context, 'Invites') : Container(),
                  _buildLabel(context, 'In-Progress'),
                  _buildLabel(context, 'Redeemed'),
                  _buildLabel(context, 'Completed'),
                ],
              ),
            ],
          ),
        ],
      ),
    );
  }

  Widget _buildStatCircle(
    BuildContext context,
    Function onTap,
    Color color,
    int count,
  ) =>
      Expanded(
        child: Center(
          child: GestureDetector(
            onTap: onTap,
            child: Stack(
              alignment: Alignment.center,
              children: <Widget>[
                Container(
                  width: 48,
                  height: 48,
                  decoration: BoxDecoration(
                    boxShadow: AppShadows.elevation[0],
                    borderRadius: BorderRadius.circular(24.0),
                    color: Theme.of(context).scaffoldBackgroundColor,
                  ),
                ),
                Container(
                  width: 36,
                  height: 36,
                  decoration: BoxDecoration(
                    borderRadius: BorderRadius.circular(24.0),
                    color: count == null ? Colors.transparent : color,
                  ),
                ),
                Text(
                  count != null ? '$count' : '0',
                  textAlign: TextAlign.center,
                  style: TextStyle(
                      color: Theme.of(context).scaffoldBackgroundColor,
                      height: 1.2,
                      fontWeight: FontWeight.w500),
                ),
              ],
            ),
          ),
        ),
      );

  Widget _buildLabel(BuildContext context, String label) => Expanded(
        child: Text(
          label,
          textAlign: TextAlign.center,
          overflow: TextOverflow.ellipsis,
          style: Theme.of(context).textTheme.caption.merge(
                TextStyle(color: Theme.of(context).hintColor, fontSize: 11.0),
              ),
        ),
      );
}

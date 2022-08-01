import 'package:flutter/material.dart';
import 'package:rxdart/rxdart.dart';
import 'package:rydr_app/app/state.dart';
import 'package:rydr_app/app/tags_config.dart';
import 'package:rydr_app/app/theme.dart';
import 'package:rydr_app/models/tag.dart';
import 'package:rydr_app/models/workspace_features.dart';

class DealInputTags extends StatelessWidget {
  final BehaviorSubject<List<Tag>> valueStream;
  final Function handleUpdate;

  DealInputTags({
    @required this.valueStream,
    @required this.handleUpdate,
  });

  /// currently only available to paid subscription accounts
  /// in team workspaces
  @override
  Widget build(BuildContext context) {
    /// get list of tags configured for use
    List<Tag> _tags = List<Tag>.from(tagsConfig.tagsDeals);

    /// add any existing tags on the deal that are not in our config
    /// this way we can guard against removing a tags from config and then not seeing it on the deal
    if (valueStream.value != null && valueStream.value.isNotEmpty) {
      _tags
        ..addAll(
            valueStream.value.where((existing) => !_exists(existing, _tags)))
        ..toList();
    }

    return WorkspaceFeatures.hasDealTags(
            appState.currentWorkspace.workspaceFeatures)
        ? Padding(
            padding: EdgeInsets.only(left: 16, right: 16, bottom: 16),
            child: StreamBuilder<List<Tag>>(
                stream: valueStream,
                builder: (context, snapshot) {
                  final List<Tag> selected = snapshot.data ?? [];

                  return Wrap(
                    children: _tags.map((Tag t) {
                      final bool isSelected = _exists(t, selected);

                      return Padding(
                          padding: EdgeInsets.only(right: 8),
                          child: ChoiceChip(
                            avatar: getTagIcon(t.value) != null
                                ? Icon(
                                    getTagIcon(t.value.toLowerCase()),
                                    size: 14,
                                    color: AppColors.white,
                                  )
                                : null,
                            label: Text(
                              t.value,
                              style: TextStyle(color: AppColors.white),
                            ),
                            selected: isSelected,
                            selectedColor: Theme.of(context).primaryColor,
                            onSelected: (s) {
                              if (isSelected) {
                                handleUpdate(List<Tag>.from(selected)
                                  ..removeWhere((s) =>
                                      s.key.toLowerCase() ==
                                          t.key.toLowerCase() &&
                                      s.value.toLowerCase() ==
                                          t.value.toLowerCase())
                                  ..toList());
                              } else {
                                handleUpdate(List<Tag>.from(selected)
                                  ..add(t)
                                  ..toList());
                              }
                            },
                          ));
                    }).toList(),
                  );
                }),
          )
        : Container();
  }

  bool _exists(Tag tag, List<Tag> selected) =>
      selected.firstWhere(
          (t) =>
              t.key.toLowerCase() == tag.key.toLowerCase() &&
              t.value.toLowerCase() == tag.value.toLowerCase(),
          orElse: () => null) !=
      null;
}

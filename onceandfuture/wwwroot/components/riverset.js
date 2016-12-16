var React = require('react');
import { connect } from 'react-redux';
import { accountSettingsHide, accountSettingsShow, addRiver, refreshAllFeeds,  } from '../actions';
import {
  SIZE_BUTTON_WIDTH,
  SIZE_BUTTON_HEIGHT,
  SIZE_COLUMN_TOP,
  SIZE_COLUMN_WIDTH,
  SIZE_SPACER_WIDTH,
  SIZE_SPACER_HEIGHT,
  SIZE_PAGE_HEADER,

  Z_INDEX_BANNER,
} from './style';
import AccountSettings from './accountsettings';
import AppBanner from './appbanner';
import River from './river';

const columnLeft =(index) => index * (SIZE_COLUMN_WIDTH + SIZE_SPACER_WIDTH) + SIZE_SPACER_WIDTH;

export const AddRiverButton = ({index, onAddRiver}) => {
  const column_style = {
    position: 'absolute',
    top: SIZE_COLUMN_TOP,
    left: columnLeft(index),
    width: SIZE_BUTTON_WIDTH,
    height: SIZE_BUTTON_HEIGHT,
    fontSize: SIZE_BUTTON_HEIGHT,

    textAlign: 'center',
    paddingTop: 13,
    cursor: 'pointer',
  };

  return <div style={column_style} onClick={onAddRiver}>
      <i className="fa fa-plus-square" />
  </div>;
};

const RiverColumn = ({index}) => {
  const style = {
    left: columnLeft(index),
    width: SIZE_COLUMN_WIDTH,
    position: 'absolute',
    top: SIZE_COLUMN_TOP,
    bottom: SIZE_SPACER_HEIGHT,
  };
  return <div style={style}>
    <River index={index} />
  </div>;
}

const RiverSetBase = ({
  user,
  rivers,
  loading,
  load_progress,
  show_settings,
  onRefresh,
  onAddRiver,
  onHideSettings,
  onShowSettings,
}) => {
  const top_bar_style = {
    position: 'fixed',
    top: 0, left: 0, width: '100%',
    zIndex: Z_INDEX_BANNER,
    height: SIZE_PAGE_HEADER,
  };

  let accountSettings = <span />;
  let onSettingsClick = onShowSettings;
  if (show_settings) {
    accountSettings = <AccountSettings />;
    onSettingsClick = onHideSettings;
  }

  return (
    <div>
      <div style={top_bar_style}>
        <AppBanner
          title='Rivers'
          loading={loading}
          load_progress={load_progress}
          onRefresh={() => onRefresh(user)}
          onSettingsClick={onSettingsClick}
          />
      </div>
      <div>
        { rivers.map((r, index) => <RiverColumn index={index} key={'r'+index} />) }
        <AddRiverButton index={rivers.length} onAddRiver={() => onAddRiver(user)} />
      </div>

      {accountSettings}
    </div>
  );
};

// VisibleRiverSet
//
const vrs_mapStateToProps = (state) => {
  return {
    user: state.user,
    rivers: state.rivers,
    loading: state.loading,
    load_progress: state.load_progress,
    show_settings: state.account_settings.visible,
  };
};
const vrs_mapDispatchToProps = (dispatch) => {
  return {
    onRefresh: function refreshIt (user) { dispatch(refreshAllFeeds(user)); },
    onAddRiver: function addIt (user) { dispatch(addRiver(user)); },
    onHideSettings: function() { dispatch(accountSettingsHide()); },
    onShowSettings: function() { dispatch(accountSettingsShow()); },
  };
};
const RiverSet = connect(
  vrs_mapStateToProps,
  vrs_mapDispatchToProps
)(
  RiverSetBase
);

export default RiverSet;

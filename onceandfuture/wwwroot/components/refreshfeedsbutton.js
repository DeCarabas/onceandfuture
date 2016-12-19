import React from 'react';
import { connect } from 'react-redux';
import { refreshAllFeeds } from '../actions';
import {
  APP_TEXT_COLOR,
  RIVER_TITLE_BACKGROUND_COLOR,
} from './style';
import IconButton from './iconbutton';


const RefreshFeedsButtonBase = ({loading, onRefresh, user}) => {
  const onClick = loading ? () => {} : () => onRefresh(user);
  const style = {
    color: loading ? RIVER_TITLE_BACKGROUND_COLOR : APP_TEXT_COLOR
  };

  return <div style={style}>
    <IconButton onClick={onClick} icon="fa-refresh" tip="Refresh all feeds." tipPosition="bottomleft" />
  </div>;
}

const mapStateToProps = (state) => {
  return {
    user: state.user,
    loading: state.loading,
  };
};
const mapDispatchToProps = (dispatch) => {
  return {
    onRefresh: (user) => dispatch(refreshAllFeeds(user)),
  };
};
const RefreshFeedsButton = connect(mapStateToProps, mapDispatchToProps)(RefreshFeedsButtonBase);

export default RefreshFeedsButton;
